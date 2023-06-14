
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UBS.Shared
{
    [BuildStepDescriptionAttribute("Moves any debug files and folders to a specific directory above build dir")]
    [BuildStepParameterFilterAttribute(BuildStepParameterType.String)]
    public class MoveDebugFilesFromBuildFolder : IBuildStepProvider
    {
		
        #region MoveDebugFilesFromBuildFolder implementation

        // Any folders containing the following will be moved to a parent folder
        private static string[] s_DirectoryNameContentsToFilter = new string[] { "donotship", "dontship" };
        private const string DEBUG_OUTPUT_PREFIX = "_DebugInfo";

        private bool mDone = false;

        private BuildConfiguration m_BuildConfiguration;
        private double m_StartProcessTime;
        private const float DELAY_FOR_PROCESS = 10;
        
        public void BuildStepStart (BuildConfiguration configuration)
        {
            m_BuildConfiguration = configuration;
            m_StartProcessTime = EditorApplication.timeSinceStartup;
        }

        private void MoveTempFolders()
        {
            BuildTargetGroup btg = UBS.Helpers.GroupFromBuildTarget(m_BuildConfiguration.GetCurrentBuildProcess().Platform);
            var currentBuildProcess = m_BuildConfiguration.GetCurrentBuildProcess();
            var outputDir = UBSProcess.GetOutputDirectory(currentBuildProcess);


            if (!outputDir.Exists)
            {
                Debug.LogWarning("Output folder doesnt exist!");
                return;
            }

            var debugInfoDirectoryName = $"{outputDir.Name}{DEBUG_OUTPUT_PREFIX}";

            EnumerationOptions enumerationOptions = new EnumerationOptions();
            var debugInfoDirSearch = outputDir.Parent.GetDirectories(debugInfoDirectoryName, enumerationOptions);

            // If doesnt already exist, create
            var debugInfoDir = debugInfoDirSearch.Length == 0
                ? outputDir.Parent.CreateSubdirectory(debugInfoDirectoryName)
                : debugInfoDirSearch[0];

            Debug.Log($"Target output dir {debugInfoDir}");
            
            // Remove all folders from debugInfoDir
            var existingDebugInfoDirectories = debugInfoDir.GetDirectories();
            foreach (var existingDebugInfoDirectory in existingDebugInfoDirectories)
            {
                try
                {
                    // Recursively delete
                    existingDebugInfoDirectory.Delete(true);
                }
                catch (Exception e)
                {
                    Debug.Log($"Cant delete existing debug info folder {existingDebugInfoDirectory.FullName} - {e}");
                }
            }
            

            var allSubDirectories = outputDir.GetDirectories();
            foreach (var subDirectory in allSubDirectories)
            {
                bool move = false;
                foreach (var testName in s_DirectoryNameContentsToFilter)
                {
                    move |= subDirectory.Name.ToLower().Contains(testName);
                }

                if (move)
                {
                    var outputPath = $"{debugInfoDir.FullName}/{subDirectory.Name}";
                    Debug.Log($"Moving folder '{subDirectory.FullName}' to '{outputPath}'");
                    try
                    {
                        Directory.Move(subDirectory.FullName, outputPath);
                        //subDirectory.MoveTo($"{debugInfoDir.FullName}{Path.PathSeparator}{subDirectory.Name}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Issue moving folder {e}");
                    }
                }
            }
        }

        public void BuildStepUpdate()
        {
            Debug.Log($"Waiting {EditorApplication.timeSinceStartup}- {m_StartProcessTime + DELAY_FOR_PROCESS} ");
            if (EditorApplication.timeSinceStartup > (m_StartProcessTime + DELAY_FOR_PROCESS))
            {
                MoveTempFolders();
                mDone = true;
            }
        }
    
		
        public bool IsBuildStepDone ()
        {
            return mDone;
        }
		
        #endregion
    }

}