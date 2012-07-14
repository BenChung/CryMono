﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using CryEngine.Async;
using CryEngine.Extensions;
using CryEngine.Native;
using CryEngine.Sandbox;
using CryEngine.Testing;
using CryEngine.Testing.Internals;

using CryEngine.Serialization;
using System.Runtime.Serialization;

namespace CryEngine.Initialization
{
    internal class ScriptManager : IScriptManagerCallback
	{
		public ScriptManager()
		{
			Instance = this;

			if (FlowNodes == null)
				FlowNodes = new List<string>();
			if (Scripts == null)
				Scripts = new List<CryScript>();

			if(!Directory.Exists(PathUtils.TempFolder))
				Directory.CreateDirectory(PathUtils.TempFolder);
			else
			{
				try
				{
					foreach(var file in Directory.GetFiles(PathUtils.TempFolder))
						File.Delete(file);
				}
				catch(UnauthorizedAccessException) { }
			}

            TestManager.Init();

			InitializeScriptDomain(true);

			Formatter = new CrySerializer();
		}

		void InitializeScriptDomain(bool initialLoad = false)
		{
			if (!initialLoad)
			{
				using (var stream = File.Create(Path.Combine(PathUtils.TempFolder, "ScriptManager.CompiledScripts.scriptdump")))
					Formatter.Serialize(stream, Scripts);

				AppDomain.Unload(ScriptDomain);
			}

			try
			{
				LoadPlugins();
			}
			catch (Exception ex)
			{
				var scriptReloadMessage = new ScriptReloadMessage(ex, !initialLoad);
				scriptReloadMessage.ShowDialog();
			}
			finally
			{
				if (!initialLoad)
				{
					PostInit();

					using (var stream = File.Open(Path.Combine(PathUtils.TempFolder, "ScriptManager.CompiledScripts.scriptdump"), FileMode.Open))
						Scripts = Formatter.Deserialize(stream) as List<CryScript>;

					ForEach(ScriptType.Any, scriptInstance =>
					{
						if (scriptInstance.ScriptId > LastScriptId)
							LastScriptId = scriptInstance.ScriptId + 1;
					});
					
					// Can in theory remove this after we've sorted out updating C++
					// references to C# scripts. (We serialize intptr's now)
					ForEach(ScriptType.CryScriptInstance, x => x.OnScriptReloadInternal());
				}
			}
		}

		public void PostInit()
		{
			// These have to be registered later on due to the flow system being initialized late.
			foreach (var node in FlowNodes)
				FlowNode.Register(node);
		}

		public void OnReload()
		{
			InitializeScriptDomain();
		}

		public void OnRevert()
		{
			// Revert to previous state
		}

		void PopulateAssemblyLookup()
		{
#if !RELEASE
			var monoDir = Path.Combine(PathUtils.EngineFolder, "Mono");
			// Doesn't exist when unit testing
			if(Directory.Exists(monoDir))
			{
				using(XmlWriter writer = XmlWriter.Create(Path.Combine(monoDir, "assemblylookup.xml")))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("AssemblyLookupTable");

					var gacFolder = Path.Combine(monoDir, "lib", "mono", "gac");
					foreach(var assemblyLocation in Directory.GetFiles(gacFolder, "*.dll", SearchOption.AllDirectories))
					{
						var separator = new [] { "__" };
						var splitParentDir = Directory.GetParent(assemblyLocation).Name.Split(separator, StringSplitOptions.RemoveEmptyEntries);

						var assembly = Assembly.Load(Path.GetFileName(assemblyLocation) + string.Format(", Version={0}, Culture=neutral, PublicKeyToken={1}", splitParentDir.ElementAt(0), splitParentDir.ElementAt(1)));

						writer.WriteStartElement("Assembly");
						writer.WriteAttributeString("name", assembly.FullName);

						foreach(var nameSpace in assembly.GetTypes().Select(t => t.Namespace).Distinct())
						{
							writer.WriteStartElement("Namespace");
							writer.WriteAttributeString("name", nameSpace);
							writer.WriteEndElement();
						}

						writer.WriteEndElement();
					}

					writer.WriteEndElement();
					writer.WriteEndDocument();
				}
			}
#endif
		}

		void LoadPlugins()
		{
            var pluginsDirectory = Path.Combine(PathUtils.ScriptsFolder, "Plugins");
            foreach (var directory in Directory.GetDirectories(pluginsDirectory))
            {
                var compilerDll = Path.Combine(directory, "Compiler.dll");
                if (File.Exists(compilerDll))
                {
                    var assembly = LoadAssembly(compilerDll);

                    var compilerType = assembly.GetTypes().First(x => x.Implements<ScriptCompiler>());
                    Debug.LogAlways("		Initializing {0} compiler...", compilerType.Name);

                    var compiler = Activator.CreateInstance(compilerType) as ScriptCompiler;

                    var assemblyPaths = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
                    var assemblies = new List<Assembly>();

                    foreach (var assemblyPath in assemblyPaths)
                    {
                        if (assemblyPath != compilerDll)
                            assemblies.Add(LoadAssembly(assemblyPath));
                    }

                    foreach (var unprocessedScript in compiler.Process(assemblies))
                    {
                        var script = unprocessedScript;

                        if (script.RegistrationParams is ActorRegistrationParams)
                        {
                            var registrationParams = (ActorRegistrationParams)script.RegistrationParams;

                            if (registrationParams.useMonoActor)
                                NativeMethods.Actor.RegisterClass(script.ScriptName, registrationParams.isAI);
                        }
                        else if (script.RegistrationParams is EntityRegistrationParams)
                        {
                            var registrationParams = (EntityRegistrationParams)script.RegistrationParams;

                            if (registrationParams.name == null)
                                registrationParams.name = script.ScriptName;

                            NativeMethods.Entity.RegisterClass(registrationParams);
                        }
                        else if (script.RegistrationParams is GameRulesRegistrationParams)
                        {
                            var registrationParams = (GameRulesRegistrationParams)script.RegistrationParams;

                            if (registrationParams.name == null)
                                registrationParams.name = script.ScriptName;

                            NativeMethods.GameRules.RegisterGameMode(registrationParams.name);

                            if (registrationParams.defaultGamemode)
                                NativeMethods.GameRules.SetDefaultGameMode(registrationParams.name);
                        }
                        else if (script.RegistrationParams is FlowNodeRegistrationParams)
                        {
                            var registrationParams = (FlowNodeRegistrationParams)script.RegistrationParams;

                            if (registrationParams.name == null)
                                registrationParams.name = script.ScriptName;
                            if (registrationParams.category == null)
                                registrationParams.category = script.Type.Namespace;

                            script.ScriptName = registrationParams.category + ":" + registrationParams.name;

                            FlowNodes.Add(script.ScriptName);
                        }

                        Scripts.Add(script);
                    }
                }
            }
		}

		/// <summary>
		/// Loads a C# assembly by location, creates a shadow-copy and generates debug database (mdb).
		/// </summary>
		/// <param name="assemblyPath"></param>
		public Assembly LoadAssembly(string assemblyPath)
		{
			if(assemblyPath == null)
				throw new ArgumentNullException("assemblyPath");
			if(assemblyPath.Length < 1)
				throw new ArgumentException("string cannot be empty!", "assemblyPath");

			var newPath = Path.Combine(PathUtils.TempFolder, Path.GetFileName(assemblyPath));

			TryCopyFile(assemblyPath, ref newPath);

#if !RELEASE
			GenerateDebugDatabaseForAssembly(assemblyPath);

			var mdbFile = assemblyPath + ".mdb";
			if(File.Exists(mdbFile)) // success
			{
				var newMdbPath = Path.Combine(PathUtils.TempFolder, Path.GetFileName(mdbFile));
				TryCopyFile(mdbFile, ref newMdbPath);
			}
#endif

			return Assembly.LoadFrom(newPath);
		}

		void TryCopyFile(string currentPath, ref string newPath, bool overwrite = true)
		{
			if(!File.Exists(newPath))
				File.Copy(currentPath, newPath, overwrite);
			else
			{
				try
				{
					File.Copy(currentPath, newPath, overwrite);
				}
				catch(Exception ex)
				{
					if(ex is UnauthorizedAccessException || ex is IOException)
					{
						newPath = Path.ChangeExtension(newPath, "_" + Path.GetExtension(newPath));
						TryCopyFile(currentPath, ref newPath);
					}
					else
						throw;
				}
			}
		}

		public void GenerateDebugDatabaseForAssembly(string assemblyPath)
		{
			if(assemblyPath == null)
				throw new ArgumentNullException("assemblyPath");
			if(assemblyPath.Length < 1)
				throw new ArgumentException("string cannot be empty!", "assemblyPath");

			if(File.Exists(Path.ChangeExtension(assemblyPath, "pdb")))
			{
				var assembly = Assembly.LoadFrom(Path.Combine(PathUtils.EngineFolder, "Mono", "bin", "pdb2mdb.dll"));
				var driver = assembly.GetType("Driver");
				var convertMethod = driver.GetMethod("Convert", BindingFlags.Static | BindingFlags.Public);

				object[] args = { assemblyPath };
				convertMethod.Invoke(null, args);
			}
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		public void OnUpdate(float frameTime, float frameStartTime, float asyncTime, float frameRate, float timeScale)
		{
			Time.Set(frameTime, frameStartTime, asyncTime, frameRate, timeScale);

			Awaiter.Instance.OnUpdate(frameTime);

			Scripts.ForEach(x =>
			{
				if (x.ScriptType.ContainsFlag(ScriptType.CryScriptInstance) && x.ScriptInstances != null)
				{
					x.ScriptInstances.ForEach(instance =>
					{
						if (instance.ReceiveUpdates)
							instance.OnUpdate();
					});
				}
			});
		}

		/// <summary>
		/// Instantiates a script using its name and interface.
		/// </summary>
		/// <param name="scriptName"></param>
		/// <param name="constructorParams"></param>
		/// <returns>New instance scriptId or -1 if instantiation failed.</returns>
		public CryScriptInstance CreateScriptInstance(string scriptName, ScriptType scriptType, object[] constructorParams = null)
		{
			if (scriptName == null)
				throw new ArgumentNullException("scriptName");
			if (scriptName.Length < 1)
				throw new ArgumentException("string cannot be empty!", "scriptName");
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			var script = Scripts.FirstOrDefault(x => x.ScriptType.ContainsFlag(scriptType) && x.ScriptName.Equals(scriptName));
			if (script == default(CryScript))
				throw new ScriptNotFoundException(string.Format("Script {0} of ScriptType {1} could not be found.", scriptName, scriptType));

			var scriptInstance = Activator.CreateInstance(script.Type, constructorParams) as CryScriptInstance;
			if (scriptInstance == null)
				throw new ArgumentException("Failed to create instance, make sure type derives from CryScriptInstance", "scriptName");

			if (scriptType == ScriptType.GameRules)
				GameRules.Current = scriptInstance as GameRules;

			AddScriptInstance(script, scriptInstance);

			return scriptInstance;
		}

		public void AddScriptInstance(CryScriptInstance instance, ScriptType scriptType)
		{
			if (instance == null)
				throw new ArgumentNullException("instance");
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			var script = FindScript(scriptType, x => x.Type == instance.GetType());
			if (script == default(CryScript))
			{
				if (CryScript.TryCreate(instance.GetType(), out script))
					Scripts.Add(script);
				else
					return;
			}

			AddScriptInstance(script, instance);
		}

		void AddScriptInstance(CryScript script, CryScriptInstance instance)
		{
			if (script == default(CryScript))
				throw new ArgumentException("script");

			var index = Scripts.IndexOf(script);
			if (index == -1)
				throw new ArgumentException("Provided CryScript object was not present in the script collection", "script");

			instance.ScriptId = LastScriptId++;

			if (script.ScriptInstances == null)
				script.ScriptInstances = new List<CryScriptInstance>();

			script.ScriptInstances.Add(instance);

			Scripts[index] = script;
		}

		public void RemoveInstance(int instanceId, ScriptType scriptType)
		{
			RemoveInstances<CryScriptInstance>(scriptType, x => x.ScriptId == instanceId);
		}

		/// <summary>
		/// Locates and destructs the script with the assigned scriptId.
		/// </summary>
		public int RemoveInstances<T>(ScriptType scriptType, Predicate<T> match) where T : CryScriptInstance
		{
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			int numRemoved = 0;
			for (int i = 0; i < Scripts.Count; i++)
			{
				var script = Scripts[i];

				if (script.ScriptType.ContainsFlag(scriptType))
				{
					if (script.ScriptInstances != null)
						numRemoved += script.ScriptInstances.RemoveAll(x => match(x as T));
				}

				Scripts[i] = script;
			}

			return numRemoved;
		}

		public int RemoveInstances(ScriptType scriptType, Predicate<CryScriptInstance> match)
		{
			return RemoveInstances<CryScriptInstance>(scriptType, match);
		}

		public CryScriptInstance GetScriptInstanceById(int id, ScriptType scriptType)
		{
			if (id == 0)
				throw new ArgumentException("instance id cannot be 0!");

			return Find<CryScriptInstance>(scriptType, x => x.ScriptId == id);
		}

		#region Linq statements
		public CryScript FindScript(ScriptType scriptType, Func<CryScript, bool> predicate)
		{
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			return Scripts.FirstOrDefault(x => x.ScriptType.ContainsFlag(scriptType) && predicate(x));
		}

		public void ForEachScript(ScriptType scriptType, Action<CryScript> action)
		{
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			Scripts.ForEach(x =>
			{
				if (x.ScriptType.ContainsFlag(scriptType))
					action(x);
			});
		}

		public void ForEach(ScriptType scriptType, Action<CryScriptInstance> action)
		{
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			ForEachScript(scriptType, script =>
			{
				if (script.ScriptInstances != null)
					script.ScriptInstances.ForEach(action);
			});
		}

		public T Find<T>(ScriptType scriptType, Func<T, bool> predicate) where T : CryScriptInstance
		{
			if (!Enum.IsDefined(typeof(ScriptType), scriptType))
				throw new ArgumentException(string.Format("scriptType: value {0} was not defined in the enum", scriptType));

			T scriptInstance = null;

			ForEachScript(scriptType, script =>
			{
				if (script.ScriptInstances != null && script.Type.ImplementsOrEquals<T>())
				{
					var instance = script.ScriptInstances.Find(x => predicate(x as T)) as T;
					if (instance != null)
					{
						scriptInstance = instance;
						return;
					}
				}
			});

			return scriptInstance;
		}
		#endregion

		/// <summary>
		/// Last assigned ScriptId, next = + 1
		/// </summary>
		public int LastScriptId = 1;

		public bool IgnoreExternalCalls { get; set; }

		internal List<CryScript> Scripts { get; set; }

		AppDomain ScriptDomain { get; set; }
		IFormatter Formatter { get; set; }

		List<string> FlowNodes { get; set; }

		public static ScriptManager Instance;
	}

	[Serializable]
	public class ScriptNotFoundException : Exception
	{
		public ScriptNotFoundException(string error)
		{
			message = error;
		}

		private string message;
		public override string Message
		{
			get { return message; }
		}
	}
}