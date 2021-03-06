﻿#region license

// Copyright (c) 2011 Michael Thomas
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Threading;
using Prudence.Configuration;
using log4net;

namespace Prudence
{
    public class ConsoleApplicationHost
    {
        private const int PollingIntervalMiliseconds = 100;

        private readonly ILog _log = LogManager.GetLogger("ConsoleApplicationHost");

        private ApplicationComponent _component;
        private bool _done;

        public void Run(string[] args, ApplicationComponent component)
        {
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

            var configService = new ConfigurationService();

            configService.Init(@"C:\PrudenceInstallation\prudence.json"); //TODO

            Console.CancelKeyPress += ConsoleCancelKeyPress;

            _component = component;

            _component.Init(configService.GetConfiguration());

            _component.Start();

            while (!_done)
            {
                Thread.Sleep(PollingIntervalMiliseconds);
            }

            _log.InfoFormat("Exiting");
        }

        private void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal("Unhandled exception in console application host", e.ExceptionObject as Exception);
        }

        private void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _log.Info("Recieved Ctrl+C triggering stop on application component");

            e.Cancel = true;

            _component.Stop();

            _done = true;
        }
    }
}