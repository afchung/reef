﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Com.Microsoft.Tang.Implementations;
using Com.Microsoft.Tang.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Com.Microsoft.TangTest.Tang
{
    [TestClass]
    public class Injection
    {
        static string file = @"Com.Microsoft.Tang.Examples.dll";
        static Assembly asm = null;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            asm = Assembly.LoadFrom(file);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            System.Console.WriteLine("ClassCleanup");
        }

        [TestInitialize()]
        public void TestSetup()
        {
            System.Console.WriteLine("TestSetup");
        }

        [TestCleanup()]
        public void TestCleanup()
        {
            System.Console.WriteLine("TestCleanup");
        }

        [TestMethod]
        public void TestTimer()
        {
            Type timerType = typeof(Com.Microsoft.Tang.Examples.Timer);
            Type namedParameter = asm.GetType(@"Com.Microsoft.Tang.Examples.Timer+Seconds");

            ITang tang = TangFactory.GetTang();
            ICsConfigurationBuilder cb = tang.NewConfigurationBuilder(new string[] { file });
            cb.BindNamedParameter(namedParameter, "2");
            IConfiguration conf = cb.Build();
            IInjector injector = tang.NewInjector(conf);
            var timer = (Com.Microsoft.Tang.Examples.Timer)injector.GetInstance(timerType);

            Assert.IsNotNull(timer);

            System.Console.WriteLine("Tick...");
            timer.sleep();
            System.Console.WriteLine("Tock...");
        }

        [TestMethod]
        public void TestDocumentLoadNamedParameter()
        {
            Type documentedLocalNamedParameterType = typeof(Com.Microsoft.Tang.Examples.DocumentedLocalNamedParameter);
            Type namedParameter = asm.GetType(@"Com.Microsoft.Tang.Examples.DocumentedLocalNamedParameter+Foo");

            ITang tang = TangFactory.GetTang();
            ICsConfigurationBuilder cb = tang.NewConfigurationBuilder(new string[] { file });
            cb.BindNamedParameter(namedParameter, "Hello");
            IConfiguration conf = cb.Build();
            IInjector injector = tang.NewInjector(conf);
            var doc = (Com.Microsoft.Tang.Examples.DocumentedLocalNamedParameter)injector.GetInstance(documentedLocalNamedParameterType);

            Assert.IsNotNull(doc);
            var s = doc.ToString();
        }

        [TestMethod]
        public void TestSimpleConstructor()
        {
            Type simpleConstructorType = typeof(Com.Microsoft.Tang.Examples.SimpleConstructors);

            ITang tang = TangFactory.GetTang();
            ICsConfigurationBuilder cb = tang.NewConfigurationBuilder(new string[] { file });
            IConfiguration conf = cb.Build();
            IInjector injector = tang.NewInjector(conf);
            var simpleConstructor = (Com.Microsoft.Tang.Examples.SimpleConstructors)injector.GetInstance(simpleConstructorType);
            Assert.IsNotNull(simpleConstructor);
        }
    }
}
