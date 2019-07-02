﻿// ***********************************************************************
// Copyright (c) 2011 Charlie Poole, Rob Prouse
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
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace NUnit.Engine
{
    /// <summary>
    /// TestEngineActivator creates an instance of the test engine and returns an ITestEngine interface.
    /// </summary>
    public static class TestEngineActivator
    {
        internal static readonly Version DefaultMinimumVersion = new Version(3, 0);

        private const string DefaultAssemblyName = "nunit.engine.dll";
        internal const string DefaultTypeName = "NUnit.Engine.TestEngine";

#if NETSTANDARD1_6
        /// <summary>
        /// Create an instance of the test engine.
        /// </summary>
        /// <returns>An <see cref="NUnit.Engine.ITestEngine"/></returns>
        public static ITestEngine CreateInstance()
        {
            var apiLocation = typeof(TestEngineActivator).GetTypeInfo().Assembly.Location;
            var directoryName = Path.GetDirectoryName(apiLocation);
            var enginePath = directoryName == null ? DefaultAssemblyName : Path.Combine(directoryName, DefaultAssemblyName);
            var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(enginePath);
            var assembly = Assembly.Load(assemblyName);
            var engineType = assembly.GetType(DefaultTypeName);
            return Activator.CreateInstance(engineType) as ITestEngine;
        }
#elif NETSTANDARD2_0
        /// <summary>
        /// Create an instance of the test engine.
        /// </summary>
        /// <returns>An <see cref="NUnit.Engine.ITestEngine"/></returns>
        public static ITestEngine CreateInstance()
        {
            var apiLocation = typeof(TestEngineActivator).Assembly.Location;
            var directoryName = Path.GetDirectoryName(apiLocation);
            var enginePath = directoryName == null ? DefaultAssemblyName : Path.Combine(directoryName, DefaultAssemblyName);
            var assembly = Assembly.LoadFrom(enginePath);
            var engineType = assembly.GetType(DefaultTypeName);
            return Activator.CreateInstance(engineType) as ITestEngine;
        }
#else

        /// <summary>
        /// Create an instance of the test engine.
        /// </summary>
        /// <param name="unused">This parameter is no longer used but has not been removed to ensure API compatibility.</param>
        /// <exception cref="NUnitEngineNotFoundException">Thrown when a test engine of the required minimum version is not found</exception>
        /// <returns>An <see cref="NUnit.Engine.ITestEngine"/></returns>
        public static ITestEngine CreateInstance(bool unused = false)
        {
            return CreateInstance(DefaultMinimumVersion, unused);
        }

        /// <summary>
        /// Create an instance of the test engine with a minimum version.
        /// </summary>
        /// <param name="minVersion">The minimum version of the engine to return inclusive.</param>
        /// <param name="unused">This parameter is no longer used but has not been removed to ensure API compatibility.</param>
        /// <exception cref="NUnitEngineNotFoundException">Thrown when a test engine of the required minimum version is not found</exception>
        /// <returns>An <see cref="ITestEngine"/></returns>
        public static ITestEngine CreateInstance(Version minVersion, bool unused = false)
        {
            try
            {
                Assembly engine = FindNewestEngine(minVersion);
                if (engine == null)
                {
                    throw new NUnitEngineNotFoundException(minVersion);
                }
                return (ITestEngine)AppDomain.CurrentDomain.CreateInstanceFromAndUnwrap(engine.CodeBase, DefaultTypeName);
            }
            catch (NUnitEngineNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load the test engine", ex);
            }
        }

        private static Assembly FindNewestEngine(Version minVersion)
        {
            var newestVersionFound = new Version();

            // Check the Application BaseDirectory
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultAssemblyName);
            Assembly newestAssemblyFound = CheckPathForEngine(path, minVersion, ref newestVersionFound, null);

            // Check Probing Path is not found in Base Directory. This
            // allows the console or other runner to be executed with
            // a different application base and still function. In
            // particular, we do this in some tests of NUnit.
            if (newestAssemblyFound == null && AppDomain.CurrentDomain.RelativeSearchPath != null)
            {
                foreach (string relpath in AppDomain.CurrentDomain.RelativeSearchPath.Split(new char[] { ';' }))
                {
                    path = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relpath), DefaultAssemblyName);
                    newestAssemblyFound = CheckPathForEngine(path, minVersion, ref newestVersionFound, null);
                    if (newestAssemblyFound != null)
                        break;
                }
            }

            return newestAssemblyFound;
        }

        private static Assembly CheckPathForEngine(string path, Version minVersion, ref Version newestVersionFound, Assembly newestAssemblyFound)
        {
            try
            {
                if (path != null && File.Exists(path))
                {
                    var ass = Assembly.ReflectionOnlyLoadFrom(path);
                    var ver = ass.GetName().Version;
                    if (ver >= minVersion && ver > newestVersionFound)
                    {
                        newestVersionFound = ver;
                        newestAssemblyFound = ass;
                    }
                }
            }
            catch (Exception){}
            return newestAssemblyFound;
        }

        private static string FindEngineInRegistry(RegistryKey rootKey, string subKey)
        {
            try
            {
                using (var key = rootKey.OpenSubKey(subKey, false))
                {
                    if (key != null)
                    {
                        Version newest = null;
                        string[] subkeys = key.GetValueNames();
                        foreach (string name in subkeys)
                        {
                            try
                            {
                                var current = new Version(name);
                                if (newest == null || current.CompareTo(newest) > 0)
                                {
                                    newest = current;
                                }
                            }
                            catch (Exception){}
                        }
                        if(newest != null)
                            return key.GetValue(newest.ToString()) as string;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
#endif
    }
}
