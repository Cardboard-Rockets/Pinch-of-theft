using UnityEditor;

namespace PinchOfTheft.Editor
{
    // A single setup path: it reuses House meshes/materials and adds only the laptop table.
    public static class VanIntroSetup
    {
        public const string ScenePath = "Assets/Scenes/Intro_Van.unity";

        [MenuItem("PinchOfTheft/Intro/Create Or Rebuild Opening")]
        public static void Build() => RearWindowIntroSetup.Build();

        [MenuItem("PinchOfTheft/Intro/Render And Check Opening")]
        public static void CaptureAndCheck() => RearWindowIntroSetup.CaptureAndCheck();
    }
}
