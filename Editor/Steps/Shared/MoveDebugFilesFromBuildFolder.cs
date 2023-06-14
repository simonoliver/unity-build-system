
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
        // We need to wait a moment after build, or access to files won't be possible
        private const float DELAY_FOR_PROCESS = 1;

        private DirectoryInfo m_OutputDir;
        private DirectoryInfo m_DebugInfoDir;
        
        public void BuildStepStart (BuildConfiguration configuration)
        {
            m_BuildConfiguration = configuration;
            m_StartProcessTime = EditorApplication.timeSinceStartup;
            
            BuildTargetGroup btg = UBS.Helpers.GroupFromBuildTarget(m_BuildConfiguration.GetCurrentBuildProcess().Platform);
            var currentBuildProcess = m_BuildConfiguration.GetCurrentBuildProcess();
            m_OutputDir = UBSProcess.GetOutputDirectory(currentBuildProcess);


            if (!m_OutputDir.Exists)
            {
                Debug.LogWarning("Output folder doesnt exist!");
                mDone = true;
                return;
            }
            // Create target folder
            var debugInfoDirectoryName = $"{m_OutputDir.Name}{DEBUG_OUTPUT_PREFIX}";

            EnumerationOptions enumerationOptions = new EnumerationOptions();
            var debugInfoDirSearch = m_OutputDir.Parent.GetDirectories(debugInfoDirectoryName, enumerationOptions);

            // If doesnt already exist, create
            m_DebugInfoDir = debugInfoDirSearch.Length == 0
                ? m_OutputDir.Parent.CreateSubdirectory(debugInfoDirectoryName)
                : debugInfoDirSearch[0];

            Debug.Log($"Target output dir {m_DebugInfoDir}");
            
            // Remove all folders from debugInfoDir
            var existingDebugInfoDirectories = m_DebugInfoDir.GetDirectories();
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
        }

        private bool MoveTempFolders()
        {
            
            var allSubDirectories = m_OutputDir.GetDirectories();
            foreach (var subDirectory in allSubDirectories)
            {
                bool move = false;
                foreach (var testName in s_DirectoryNameContentsToFilter)
                {
                    move |= subDirectory.Name.ToLower().Contains(testName);
                }

                if (move)
                {
                    var outputPath = $"{m_DebugInfoDir.FullName}/{subDirectory.Name}";
                    Debug.Log($"Moving folder '{subDirectory.FullName}' to '{outputPath}'");
                    try
                    {
                        Directory.Move(subDirectory.FullName, outputPath);
                        //subDirectory.MoveTo($"{debugInfoDir.FullName}{Path.PathSeparator}{subDirectory.Name}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Issue moving folder {e}");
                        // We might have an issue moving, so will hold off as might have data acccess issues
                        // (FIle still open)
                        return false;
                    }
                }
            }

            return true;
        }

        public void BuildStepUpdate()
        {
            //Debug.Log($"Waiting {EditorApplication.timeSinceStartup}- {m_StartProcessTime + DELAY_FOR_PROCESS} ");
            if (EditorApplication.timeSinceStartup > (m_StartProcessTime + DELAY_FOR_PROCESS))
            {
                var success = MoveTempFolders();
                if (success)
                {
                    mDone = true;
                }
                else
                {
                    // Delay again
                    m_StartProcessTime += DELAY_FOR_PROCESS;
                }
            }
        }
    
		
        public bool IsBuildStepDone ()
        {
            return mDone;
        }
		
        #endregion
    }

}