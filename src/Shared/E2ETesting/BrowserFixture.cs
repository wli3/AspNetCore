// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.E2ETesting
{
    public class BrowserFixture : IDisposable
    {
        public BrowserFixture(IMessageSink diagnosticsMessageSink)
        {
            DiagnosticsMessageSink = diagnosticsMessageSink;

            if (!HostSupportsBrowserAutomation)
            {
                DiagnosticsMessageSink.OnMessage(new DiagnosticMessage("Host does not support browser automation."));
                return;
            }

            var opts = new ChromeOptions();

            // Comment this out if you want to watch or interact with the browser (e.g., for debugging)
            opts.AddArgument("--headless");

            // Log errors
            opts.SetLoggingPreference(LogType.Browser, LogLevel.All);

            // On Windows/Linux, we don't need to set opts.BinaryLocation
            // But for Travis Mac builds we do
            var binaryLocation = Environment.GetEnvironmentVariable("TEST_CHROME_BINARY");
            if (!string.IsNullOrEmpty(binaryLocation))
            {
                opts.BinaryLocation = binaryLocation;
                DiagnosticsMessageSink.OnMessage(new DiagnosticMessage($"Set {nameof(ChromeOptions)}.{nameof(opts.BinaryLocation)} to {binaryLocation}"));
            }

            try
            {
                var driver = new RemoteWebDriver(SeleniumStandaloneServer.Instance.Uri, opts);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);
                Browser = driver;
                Logs = new RemoteLogs(driver);
            }
            catch (WebDriverException ex)
            {
                var message =
                    "Failed to connect to the web driver. Please see the readme and follow the instructions to install selenium." +
                    "Remember to start the web driver with `selenium-standalone start` before running the end-to-end tests.";
                throw new InvalidOperationException(message, ex);
            }
        }

        public IWebDriver Browser { get; }

        public ILogs Logs { get; }

        public IMessageSink DiagnosticsMessageSink { get; }

        public static bool HostSupportsBrowserAutomation => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_BROWSER_AUTOMATION_DISABLED")) &&
            (IsAppVeyor || (IsVSTS && RuntimeInformation.OSDescription.Contains("Microsoft Windows")) || OSSupportsEdge());

        private static bool IsAppVeyor =>
            Environment.GetEnvironmentVariables().Contains("APPVEYOR");

        private static bool IsVSTS =>
            Environment.GetEnvironmentVariables().Contains("TF_BUILD");

        private static int GetWindowsVersion()
        {
            var osDescription = RuntimeInformation.OSDescription;
            var windowsVersion = Regex.Match(osDescription, "^Microsoft Windows (\\d+)\\..*");
            return windowsVersion.Success ? int.Parse(windowsVersion.Groups[1].Value) : -1;
        }

        private static bool OSSupportsEdge()
        {
            var windowsVersion = GetWindowsVersion();
            return (windowsVersion >= 10 && windowsVersion < 2000)
                || (windowsVersion >= 2016);
        }

        public void Dispose()
        {
            if (Browser != null)
            {
                Browser.Dispose();
            }
        }
    }
}
