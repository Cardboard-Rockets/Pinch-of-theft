using System.IO;
using UnityEditor;
using UnityEngine;
public static class ProjectMigration
{
    public static void Move()
    {
        const string root="Assets/PinchOfTheft";
        foreach(var f in new[]{"Scripts/Runtime/CCTV","Scripts/Runtime/Core","Scripts/Runtime/Interaction","Scripts/Runtime/Movement","Scripts/Runtime/Puzzle","Scripts/Editor","Prefabs/Core","Prefabs/CCTV","Prefabs/Player","Prefabs/Interaction","Prefabs/Puzzle","Prefabs/Props","Materials","Docs"})Directory.CreateDirectory(root+"/"+f);
        AssetDatabase.Refresh();
        foreach(var area in new[]{"CCTV","Movement","Interaction"})
        {
            foreach(var file in Directory.GetFiles("Assets/"+area+"/Scripts","*.cs"))
            {
                string name=Path.GetFileName(file);string category=name=="CodePuzzle.cs"||name=="KeypadUI.cs"?"Puzzle":area;
                string error=AssetDatabase.MoveAsset(file.Replace('\\','/'),root+"/Scripts/Runtime/"+category+"/"+name);if(error!="")throw new System.Exception(error);
            }
            foreach(var file in Directory.GetFiles("Assets/"+area+"/Editor","*.cs"))
            {string error=AssetDatabase.MoveAsset(file.Replace('\\','/'),root+"/Scripts/Editor/"+Path.GetFileName(file));if(error!="")throw new System.Exception(error);}
            foreach(var file in Directory.GetFiles("Assets/"+area,"*.md"))AssetDatabase.MoveAsset(file.Replace('\\','/'),root+"/Docs/"+Path.GetFileName(file));
        }
        foreach(var file in Directory.GetFiles("Assets/Movement","*.mat"))AssetDatabase.MoveAsset(file.Replace('\\','/'),root+"/Materials/"+Path.GetFileName(file));
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("MIGRATION_MOVED");
    }
}
