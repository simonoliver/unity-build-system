using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Serialization;

namespace UBS
{
    [Serializable]
    public class UBSProcess : ScriptableObject
    {
        const string ProcessPath = "Assets/UBSProcess.asset";
        const string ProcessPathKey = "UBSProcessPath";


		#region data
        

        [SerializeField]
        BuildConfiguration
            currentBuildConfiguration;
        BuildConfiguration CurrentBuildConfiguration
        {
            get { return currentBuildConfiguration;}

        }

        [FormerlySerializedAs("mBuildAndRun")] [SerializeField]
        bool
            _buildAndRun;

        [FormerlySerializedAs("mBatchMode")] [SerializeField]
        bool
            _batchMode;
        public bool IsInBatchMode
        {
            get { return _batchMode; }
        }

        [FormerlySerializedAs("mCollection")] [SerializeField]
        BuildCollection
            _collection;
        public BuildCollection BuildCollection
        {
            get { return _collection; }
        }

        [FormerlySerializedAs("mSelectedProcesses")] [SerializeField]
        List<BuildProcess>
            _selectedProcesses;


        [FormerlySerializedAs("mCurrentBuildProcessIndex")] [SerializeField]
        int
            _currentBuildProcessIndex;

        [FormerlySerializedAs("mCurrentState")] [SerializeField]
        UBSState
            _currentState = UBSState.invalid;

        [FormerlySerializedAs("mPreStepWalker")] [SerializeField]
        UBSStepListWalker
            _preStepWalker = new UBSStepListWalker();

        [FormerlySerializedAs("mPostStepWalker")] [SerializeField]
        UBSStepListWalker
            _postStepWalker = new UBSStepListWalker();

        public UBSState CurrentState
        {
            get { return _currentState; }
        }

        public UBSStepListWalker SubPreWalker
        {
            get
            {
                return _preStepWalker;
            }
        }

        public UBSStepListWalker SubPostWalker
        {
            get
            {
                return _postStepWalker;
            }
        }

        public float Progress
        {
            get
            {

                return ((SubPreWalker.Progress + SubPostWalker.Progress) / 2.0f
                    + System.Math.Max(0, _currentBuildProcessIndex - 1)) / (float)_selectedProcesses.Count;
            }
        }

        public string CurrentProcessName
        {
            get
            {
                if(CurrentProcess != null)
                {
                    return CurrentProcess.Name;
                }
                return "N/A";
            }
        }

        BuildProcess CurrentProcess
        {
            get
            {
                if(_selectedProcesses == null || _currentBuildProcessIndex >= _selectedProcesses.Count)
                {
                    return null;
                }
                return _selectedProcesses[_currentBuildProcessIndex];
            }
        }

        public bool IsDone { get
            {
                return CurrentState == UBSState.done || CurrentState == UBSState.aborted;
            }
        }

        #endregion

        #region public interface

        public BuildProcess GetCurrentProcess()
        {
            return CurrentProcess;
        }

        public static string GetProcessPath()
        {
            return EditorPrefs.GetString(ProcessPathKey, ProcessPath);
        }
        /// <summary>
        /// You can overwrite where to store the build process. 
        /// </summary>
        /// <param name="pPath">P path.</param>
        public static void SetProcessPath(string pPath)
        {
            EditorPrefs.GetString(ProcessPathKey, pPath);
        }

		#region command line options

        /// <summary>
        /// Builds a given build collection from command line. Call this method directly from the command line using Unity in headless mode. 
        /// <https://docs.unity3d.com/Documentation/Manual/CommandLineArguments.html>
        /// 
        /// Provide `collection` parameter to your command line build to specify the collection you want to build. 
        /// All selected build processes within the collection will be build. 
        /// 
        /// Example: -collection "Assets/New\ BuildCollection.asset"
        /// </summary>
        public static void BuildFromCommandLine()
        {
            
            string[] arguments = System.Environment.GetCommandLineArgs();
            CommandLineArgsParser parser = new CommandLineArgsParser(arguments);
            foreach (var argument in parser.Collection.Arguments)
            {
	            UnityEngine.Debug.Log(argument.Name + " -> " + argument.Value);  
            } 

            string[] availableArgs = { 
                "-batchmode", 
                "-collection=", 
                "-android-sdk=", 
                "-android-ndk=", 
                "-jdk-path=", 
                "-buildTag=", 
                "-buildAll", 
                "-commitID=", 
                "-clean", // force clean builds on
                "-noclean", // force clean builds off
                "-tagName=", 
                "-buildProcessByNames="
                
            };
            
            bool batchMode = parser.Collection.HasArgument("batchmode");
            CleanBuildArgument clean = CleanBuildArgument.NotAssigned;
            if(parser.Collection.HasArgument("clean"))
            {
	            clean = CleanBuildArgument.Clean;
            }else if (parser.Collection.HasArgument("noclean"))
            {
	            clean = CleanBuildArgument.NoClean;
            }

            string collectionPath = parser.Collection.GetValue<string>("collection");
			string androidSdkPath = parser.Collection.GetValue<string>("android-sdk");
			string buildTag = parser.Collection.GetValue<string>("buildTag");            
            string commitID = parser.Collection.GetValue<string>("commitID") ;
			string tagName = parser.Collection.GetValue<string>("tagName");
			string androidNdkPath = parser.Collection.GetValue<string>("android-ndk");
			string jdkPath = parser.Collection.GetValue<string>("jdk-path");
            bool buildAll = parser.Collection.HasArgument("buildAll");
            
            string startBuildProcessByNames = parser.Collection.GetValue<string>("buildProcessByNames");
			
			if(collectionPath == null)
			{
				Debug.LogError("NO BUILD COLLECTION SET");
                return;
			}
			
			if(!string.IsNullOrEmpty(androidSdkPath))
			{
				EditorPrefs.SetString("AndroidSdkRoot", androidSdkPath);
				Debug.Log("Set Android SDK root to: " + androidSdkPath);
			}

            if (!string.IsNullOrEmpty(androidNdkPath))
            {
                EditorPrefs.SetString("AndroidNdkRoot", androidNdkPath);
                Debug.Log("Set Android NDK root to: " + androidNdkPath);
            }

            if (!string.IsNullOrEmpty(jdkPath))
            {
                EditorPrefs.SetString("JdkPath", jdkPath);
                Debug.Log("Set JDK-Path root to: " + jdkPath);
            }
            
            if(!string.IsNullOrEmpty(commitID))
            {
                EditorPrefs.SetString("commitID", commitID);
                Debug.Log("Set commitID to: " + commitID);
            }
            
            if(!string.IsNullOrEmpty(tagName))
            {
                EditorPrefs.SetString("tagName", tagName);
                Debug.Log("Set tagName to: " + tagName);
            }

			Debug.Log("Loading Build Collection: " + collectionPath);

			// Load Build Collection
			BuildCollection collection = AssetDatabase.LoadAssetAtPath(collectionPath, typeof(BuildCollection)) as BuildCollection;
			// Run Create Command

            if (!String.IsNullOrEmpty(startBuildProcessByNames))
            {
                string[] buildProcessNameList = startBuildProcessByNames.Split(',');
                var lowerCaseTrimmedBuildProcessNameList = buildProcessNameList.Select(x => x.ToLower()).Select(x => x.Trim()).ToArray();
                Create(collection, false, lowerCaseTrimmedBuildProcessNameList, batchMode, buildTag, clean);
            }
            else
            {
                Create(collection, false, batchMode, buildAll, buildTag, clean);
            }
			
			
			UBSProcess process = LoadUBSProcess();

			try
			{
				while(true)
				{
					process.MoveNext();
					switch (process.CurrentState)
					{
						case UBSState.done:
							return;
						case UBSState.aborted:
							throw new Exception("Build aborted");
					}
				}
			}catch(Exception pException)
			{
				Debug.LogError("Build failed due to exception: ");
				Debug.LogException(pException);
				EditorApplication.Exit(1);
			}
		}

		public static string AddBuildTag(string pOutputPath, string pTag)
		{
			List<string> splittedPath = new List<string>(pOutputPath.Split('/'));

			if(splittedPath[splittedPath.Count - 1].Contains("."))
			{

				splittedPath.Insert(splittedPath.Count - 2, pTag);
			}
			else
			{
				splittedPath.Add(pTag);
			}

			splittedPath.RemoveAll((str) => {
				return string.IsNullOrEmpty(str);
			});

			return string.Join("/", splittedPath.ToArray());
		}

#endregion

		public static void Create(BuildCollection collection, bool buildAndRun, bool pBatchMode = false, bool pBuildAll = false, string buildTag = "", CleanBuildArgument clean = CleanBuildArgument.NotAssigned)
		{
			UBSProcess p = ScriptableObject.CreateInstance<UBSProcess>();
			p._buildAndRun = buildAndRun;
			p._batchMode = pBatchMode;
			p._collection = collection;
			if(clean != CleanBuildArgument.NotAssigned)
				collection.cleanBuild = clean == CleanBuildArgument.Clean;
			if(!pBuildAll)
			{
				p._selectedProcesses = p._collection.Processes.FindAll( obj => obj.Selected );
			}
			else
			{
				p._selectedProcesses = p._collection.Processes;
			}
			p._currentState = UBSState.invalid;

			if(!string.IsNullOrEmpty(buildTag))
			{
				foreach(var sp in p._selectedProcesses)
				{
					sp.OutputPath = AddBuildTag(sp.OutputPath, buildTag);
				}
			}
			collection.ActivateLogTypes();

			AssetDatabase.CreateAsset( p, GetProcessPath());
			AssetDatabase.SaveAssets();
		}

        /// <summary>
        /// Builds a buildcollection by using an array of build process names (',' seperated!)
        /// By using a list of build process names, we reconfigure and retarget the actual build collection.
        /// </summary>
        public static void Create(BuildCollection collection, bool buildAndRun, string[] namesToBuild, bool batchMode = false, string buildTag = "", CleanBuildArgument clean = CleanBuildArgument.NotAssigned)
        {
            UBSProcess p = ScriptableObject.CreateInstance<UBSProcess>();
            p._buildAndRun = buildAndRun;
            p._batchMode = batchMode;
            p._collection = collection;
            if(clean != CleanBuildArgument.NotAssigned)
				collection.cleanBuild = clean == CleanBuildArgument.Clean;
            if (namesToBuild != null && namesToBuild.Length > 0)
            {
                var selectedProcesses = p._collection.Processes
                    .Where(buildProcess => namesToBuild.Contains(buildProcess.Name.ToLower())).ToList();
                p._selectedProcesses = selectedProcesses;
            }
            else
            {
                p._selectedProcesses = p._collection.Processes;
            }
            p._currentState = UBSState.invalid;

            if (!string.IsNullOrEmpty(buildTag))
            {
                foreach (var sp in p._selectedProcesses)
                {
                    sp.OutputPath = AddBuildTag(sp.OutputPath, buildTag);
                }
            }

	        collection.ActivateLogTypes();
            
            
            AssetDatabase.CreateAsset(p, GetProcessPath());
            AssetDatabase.SaveAssets();
        }

		public static bool IsUBSProcessRunning()
		{
			var asset = AssetDatabase.LoadAssetAtPath( GetProcessPath(), typeof(UBSProcess) );
			return asset != null;
		}
		public static UBSProcess LoadUBSProcess()
		{
			var process = AssetDatabase.LoadAssetAtPath( GetProcessPath(), typeof(UBSProcess));
			return process as UBSProcess;
		}
		

		public void MoveNext()
		{

			switch(CurrentState)
			{
				case UBSState.setup: DoSetup(); break;
				case UBSState.preSteps: DoPreSteps(); break;
				case UBSState.building: DoBuilding(); break;
				case UBSState.postSteps: DoPostSteps(); break;
				case UBSState.invalid: NextBuild(); break;
			}
			if(IsDone)
				OnDone();
		}
		
		public void Cancel(string pMessage)
		{
			if(pMessage.Length > 0 && !IsInBatchMode)
			{
				Debug.LogError("UBS: " + pMessage);
				EditorUtility.DisplayDialog("UBS: Error occured!", pMessage, "Ok - my fault.");
			}
			Cancel();
		}

		public void Cancel()
		{
			Debug.LogError( $"UBS: Build Process \"{currentBuildConfiguration.GetCurrentBuildProcess().Name}\" was cancelled. ");

			SetState(UBSState.aborted);
			
			
			_preStepWalker.Clear();
			_postStepWalker.Clear();
			
			_collection.RestoreLogTypes();

			if (IsInBatchMode)
			{
				EditorApplication.Exit(1);
			}
		}

		#endregion


		#region build process state handling

		void SetState(UBSState newState)
		{
			
			if (_currentState != newState)
			{
				_currentState = newState;
				Save();
			}
		}
		
		void OnDone()
		{
			if(IsInBatchMode && _currentState == UBSState.aborted)
				EditorApplication.Exit(1);
			
			_collection.RestoreLogTypes();
			SetState(UBSState.done);
			Debug.Log("UBSProcess is done. ");
		}
		void NextBuild()
		{
			if(_currentBuildProcessIndex >= _selectedProcesses.Count)
			{
				SetState(UBSState.done);
			}else
			{
				SetState(UBSState.setup);
			}
		}

		void DoSetup()
		{
			currentBuildConfiguration = new BuildConfiguration();
            currentBuildConfiguration.Initialize();

			if(!CheckOutputPath(CurrentProcess))
				return;

			_preStepWalker.Init( CurrentProcess.PreBuildSteps, currentBuildConfiguration );

			_postStepWalker.Init(CurrentProcess.PostBuildSteps, currentBuildConfiguration );

			SetState(UBSState.preSteps);
		}

		void DoPreSteps()
		{
			_preStepWalker.MoveNext();

			if(_currentState == UBSState.aborted)
				return;

			if(_preStepWalker.IsDone())
			{
				_preStepWalker.End();

				SetState(UBSState.building);
			}
			Save();
		}

		void DoBuilding()
		{
            Debug.Log($"Dobuilding called {CurrentProcess.Name} target {CurrentProcess.OutputPath}");
            
			BuildOptions bo = CurrentProcess.Options;
			if(CurrentBuildConfiguration.GetCurrentBuildCollection().cleanBuild)
				bo &= BuildOptions.CleanBuildCache;
			
			if (_buildAndRun)
				bo |= BuildOptions.AutoRunPlayer;


			

			if (!CurrentProcess.Pretend)
			{
				var scenePaths = GetScenePaths();
				
				// IN some cases we want to clear active script defines from editor (rather than just add to them
				// with extrascriptingDefines
				var buildTargetGroup= BuildPipeline.GetBuildTargetGroup(CurrentProcess.Platform);
				var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
				var preBuildScriptingDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
				Debug.Log($"preBuildScriptingDefines {preBuildScriptingDefines}");

				var modifyDefines = CurrentProcess.ClearPlayerScriptingDefines |
				                    CurrentProcess.ScriptingDefines.Count > 0;
				if (modifyDefines)
				{
					var newDefines = new List<string>(preBuildScriptingDefines.Split(';'));
					newDefines.AddRange(CurrentProcess.ScriptingDefines);
					PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget,newDefines.ToArray());
					Debug.Log($"setting defines to {string.Join(',',newDefines)}");
				}

				// Cache status for all included addressable groups pre-build
				var currentIncludedAddressables = new Dictionary<AddressableAssetGroup, bool>();
				var allGroups = AddressableAssetSettingsDefaultObject.Settings.groups;
				foreach (var groupEntry in allGroups)
				{
					var schema = groupEntry.GetSchema<BundledAssetGroupSchema>();
					if (schema != null) currentIncludedAddressables[groupEntry] = schema.IncludeInBuild;
				}


				// Does this project use addressables?
				if (AddressableAssetSettingsDefaultObject.Settings != null)
				{
					// We'll need to apply the addressable group settings for the build
					// to set which are included
					CurrentProcess.ApplyAddressableGroupSettingsForBuild();
					
					// Build addressable content
					Debug.Log("Building addressable content...");
					AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
					if (string.IsNullOrEmpty(result.Error))
					{
						Debug.Log("Addressable content built successfully.");
					}
					else
					{
						Debug.Log($"Error building addressable content - {result.Error}");
					}
				}

				_collection.ActivateLogTypes();
				
				Debug.Log($"XXXX Building to path {CurrentProcess.OutputPath} - name {CurrentProcess.Name}");
				// Cache prebuild product name in case of override
				var preBuildProductName = PlayerSettings.productName;
				

				// Allow override if string is not empty
				if (!string.IsNullOrEmpty(CurrentProcess.ProductNameOverride))
				{
					PlayerSettings.productName = CurrentProcess.ProductNameOverride;
				}
				
				
				var preBuildScriptingBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup);


				// Allow override of build script plightcrocess
				if (CurrentProcess.ScriptingBackend != preBuildScriptingBackend)
				{
					PlayerSettings.SetScriptingBackend(buildTargetGroup, CurrentProcess.ScriptingBackend);
				}

				

				BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
				{
					scenes = scenePaths.ToArray(),
					locationPathName = CurrentProcess.OutputPath,
					target = CurrentProcess.Platform,
					options = bo
					//extraScriptingDefines = CurrentProcess.ScriptingDefines.ToArray()
				};
				BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
				//_collection.ActivateLogTypes();

				// Restore settings
				PlayerSettings.productName = preBuildProductName;
				
				// Restore scripting backend
				if (CurrentProcess.ScriptingBackend != preBuildScriptingBackend)
				{
					PlayerSettings.SetScriptingBackend(buildTargetGroup, preBuildScriptingBackend);
				}

				// Restore editor scripting symbols
				if (modifyDefines)
				{
					PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, preBuildScriptingDefines);
				}
				
				// Restore addressables state to pre-build state
				foreach (var groupEntryKeyPair in currentIncludedAddressables)
				{
					var schema = groupEntryKeyPair.Key.GetSchema<BundledAssetGroupSchema>();
					if (schema != null) schema.IncludeInBuild = groupEntryKeyPair.Value;
				}
				
				
				Debug.Log($"XXX Playerbuild Result: {report.summary.result}  output path {CurrentProcess.OutputPath} defines {preBuildScriptingDefines}");
				if (report.summary.result != BuildResult.Succeeded)
				{
					Cancel("Build failed with result: " + report.summary.result);
					return;
				}
			}

			OnBuildDone ();
		}

		private List<string> GetScenePaths()
		{
			var output = new List<string>();
			if (CurrentProcess.UseEditorScenes)
			{
				foreach (var sceneAsset in EditorBuildSettings.scenes)
				{
					if(!sceneAsset.enabled)
						continue;
					if (ReferenceEquals(null, sceneAsset.path))
						continue;
					if (string.IsNullOrEmpty(sceneAsset.path))
						continue;
					output.Add(sceneAsset.path);
				}
			}
			else
			{
				foreach (var sceneAsset in CurrentProcess.SceneAssets)
				{
					if (ReferenceEquals(null, sceneAsset))
						continue;
					var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
					if (string.IsNullOrEmpty(scenePath))
						continue;
					output.Add(scenePath);
				}
			}

			return output;

		}

		void OnBuildDone() 
		{
			SetState(UBSState.postSteps);
		}
        
		void DoPostSteps()
		{
			_postStepWalker.MoveNext();
			
			if(_currentState == UBSState.aborted)
				return;

			if(_postStepWalker.IsDone())
			{
				_postStepWalker.End();
				Debug.Log($"Build Process \"{ _postStepWalker.Configuration.GetCurrentBuildProcess().Name}\" is done. ");
				// this is invalid instead of done as we first need to check
				// if there is another build process to run.
				SetState(UBSState.invalid); 
				_currentBuildProcessIndex++;
			}
			Save();
		}
		#endregion

		void Save()
		{
			if(this != null) {
				EditorUtility.SetDirty(this);
				AssetDatabase.SaveAssets();
			}
		}


		public static DirectoryInfo GetOutputDirectory(BuildProcess process)
		{
			DirectoryInfo dir;
			if(process.Platform == BuildTarget.Android
			   || process.Platform == BuildTarget.StandaloneWindows
			   || process.Platform == BuildTarget.StandaloneWindows64
			   || process.Platform == BuildTarget.StandaloneLinux64
			   || process.Platform == BuildTarget.Switch)
				dir = new DirectoryInfo(Path.GetDirectoryName(UBS.Helpers.GetAbsolutePathRelativeToProject( process.OutputPath )));
			else
				dir = new DirectoryInfo(process.OutputPath);
			return dir;
		}
		
		bool CheckOutputPath(BuildProcess pProcess)
		{
			// We don't need an output path for a pretend build
			if (pProcess.Pretend) return true;
				
			string error = "";
			
			var outputDir=GetOutputDirectory(pProcess);
			Debug.Log($"check ouput path {outputDir}");
			
			
			if(pProcess.OutputPath.Length == 0) {
				error = "Please provide an output path.";
				Cancel(error);
				return false;
			}
			
			try
			{
				var dir = GetOutputDirectory(pProcess);
				dir.Create();

				if(!dir.Exists)
					error = "The given output path is invalid.";
			}
			catch (Exception e)
			{
				error = e.ToString();
			}
			
			if(error.Length > 0)
			{
				Cancel(error);
				return false;
			}
			return true;

		}
	}

    public enum CleanBuildArgument
    {
	    NotAssigned,
	    Clean,
	    NoClean
    }

    public enum UBSState
	{
		invalid,
		setup,
		preSteps,
		building,
		postSteps,
		done,
		aborted
	}


	[BuildStepTypeFilter(BuildStepType.invalid)]
	public class SkipBuildStepEntry : IBuildStepProvider
	{
		public void BuildStepStart(BuildConfiguration configuration)
		{
			
		}

		public void BuildStepUpdate()
		{
			
		}

		public bool IsBuildStepDone()
		{
			return true;
		}
	}

}

