/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
package org.apache.reef.vortex.driver;

import net.jcip.annotations.ThreadSafe;
import org.apache.reef.annotations.audience.DriverSide;
import org.apache.reef.tang.annotations.Parameter;
import org.apache.reef.util.Optional;
import org.apache.reef.vortex.api.FutureCallback;
import org.apache.reef.vortex.api.VortexFunction;
import org.apache.reef.vortex.api.VortexFuture;
import org.apache.reef.vortex.common.*;

import javax.inject.Inject;
import java.io.Serializable;
import java.util.*;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Default implementation of VortexMaster.
 * Uses two thread-safe data structures(pendingTasklets, runningWorkers) in implementing VortexMaster interface.
 */
@ThreadSafe
@DriverSide
final class DefaultVortexMaster implements VortexMaster {
  private final Map<Integer, VortexFutureDelegate> taskletFutureMap = new HashMap<>();
  private final AtomicInteger taskletIdCounter = new AtomicInteger();
  private final RunningWorkers runningWorkers;
  private final PendingTasklets pendingTasklets;
  private final Executor executor;

  /**
   * @param runningWorkers for managing all running workers.
   */
  @Inject
  DefaultVortexMaster(final RunningWorkers runningWorkers,
                      final PendingTasklets pendingTasklets,
                      @Parameter(VortexMasterConf.CallbackThreadPoolSize.class) final int threadPoolSize) {
    this.executor = Executors.newFixedThreadPool(threadPoolSize);
    this.runningWorkers = runningWorkers;
    this.pendingTasklets = pendingTasklets;
  }

  /**
   * Add a new tasklet to pendingTasklets.
   */
  @Override
  public <TInput extends Serializable, TOutput extends Serializable> VortexFuture<TOutput>
      enqueueTasklet(final VortexFunction<TInput, TOutput> function, final TInput input,
                     final Optional<FutureCallback<TOutput>> callback) {
    // TODO[REEF-500]: Simple duplicate Vortex Tasklet launch.
    final VortexFuture<TOutput> vortexFuture;
    final int id = taskletIdCounter.getAndIncrement();
    if (callback.isPresent()) {
      vortexFuture = new VortexFuture<>(executor, this, id, callback.get());
    } else {
      vortexFuture = new VortexFuture<>(executor, this, id);
    }

    final Tasklet tasklet = new Tasklet<>(id, function, input, vortexFuture);
    putDelegate(tasklet, vortexFuture);
    this.pendingTasklets.addLast(tasklet);

    return vortexFuture;
  }

  /**
   * Cancels tasklets on the running workers.
   */
  @Override
  public void cancelTasklet(final boolean mayInterruptIfRunning, final int taskletId) {
    this.runningWorkers.cancelTasklet(mayInterruptIfRunning, taskletId);
  }

  /**
   * Add a new worker to runningWorkers.
   */
  @Override
  public void workerAllocated(final VortexWorkerManager vortexWorkerManager) {
    runningWorkers.addWorker(vortexWorkerManager);
  }

  /**
   * Remove the worker from runningWorkers and add back the lost tasklets to pendingTasklets.
   */
  @Override
  public void workerPreempted(final String id) {
    final Optional<Collection<Tasklet>> preemptedTasklets = runningWorkers.removeWorker(id);
    if (preemptedTasklets.isPresent()) {
      for (final Tasklet tasklet : preemptedTasklets.get()) {
        pendingTasklets.addFirst(tasklet);
      }
    }
  }

  @Override
  public void workerReported(final String workerId, final WorkerReport workerReport) {
    // TODO[JIRA REEF-942]: Fix when aggregation is allowed.

    for (final TaskletReport taskletReport : workerReport.getTaskletReports()) {
      switch (taskletReport.getType()) {
      case TaskletResult:
        final TaskletResultReport taskletResultReport = (TaskletResultReport) taskletReport;

        final List<Integer> resultTaskletIds = taskletResultReport.getTaskletIds();
        runningWorkers.doneTasklets(workerId, resultTaskletIds);
        fetchDelegate(resultTaskletIds).completed(resultTaskletIds, taskletResultReport.getResult());

        break;
      case TaskletCancelled:
        final TaskletCancelledReport taskletCancelledReport = (TaskletCancelledReport) taskletReport;
        final List<Integer> cancelledIdToList = Collections.singletonList(taskletCancelledReport.getTaskletId());
        runningWorkers.doneTasklets(workerId, cancelledIdToList);
        fetchDelegate(cancelledIdToList).cancelled(taskletCancelledReport.getTaskletId());

        break;
      case TaskletFailure:
        final TaskletFailureReport taskletFailureReport = (TaskletFailureReport) taskletReport;

        final List<Integer> failureTaskletIds = taskletFailureReport.getTaskletIds();
        runningWorkers.doneTasklets(workerId, taskletFailureReport.getTaskletIds());
        fetchDelegate(failureTaskletIds).threwException(failureTaskletIds, taskletFailureReport.getException());

        break;
      default:
        throw new RuntimeException("Unknown Report");
      }
    }
  }

  /**
   * Terminate the job.
   */
  @Override
  public void terminate() {
    runningWorkers.terminate();
  }

  private synchronized void putDelegate(final Tasklet tasklet, final VortexFutureDelegate delegate) {
    taskletFutureMap.put(tasklet.getId(), delegate);
  }

  private synchronized VortexFutureDelegate fetchDelegate(final List<Integer> taskletIds) {
    synchronized (taskletFutureMap) {
      VortexFutureDelegate delegate = null;
      for (final int taskletId : taskletIds) {
        final VortexFutureDelegate currDelegate = taskletFutureMap.remove(taskletId);
        if (currDelegate == null) {
          // TODO[JIRA REEF-500]: Consider duplicate tasklets.
          throw new RuntimeException("Tasklet should only be removed once.");
        }

        if (delegate == null) {
          delegate = currDelegate;
        } else {
          assert delegate == currDelegate;
        }
      }

      return delegate;
    }
  }

}
