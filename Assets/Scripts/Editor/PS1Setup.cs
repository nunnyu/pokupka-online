using UnityEditor;
using UnityEngine;

namespace PkillGame.Editor
{
    // pkill-9 menu → Create PS1 Post-Process Material
    // Run this once after importing the project. Then follow the console instructions.
    public static class PS1Setup
    {
        [MenuItem("pkill-9/Create PS1 Post-Process Material")]
        static void CreatePS1Material()
        {
            const string shaderName = "pkill9/PS1Post";
            const string matPath    = "Assets/Materials/PS1PostProcess.mat";

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[PS1Setup] Shader '{shaderName}' not found.\n" +
                               "Make sure Assets/Shaders/PS1Post.shader is imported.");
                return;
            }

            var mat = new Material(shader);
            mat.SetFloat("_PixelRows",   240f);
            mat.SetFloat("_ColorBits",   5f);
            mat.SetFloat("_ScanlineStr", 0.15f);
            mat.SetFloat("_DitherStr",   0.85f);

            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);

            Debug.Log(
                "[PS1Setup] Created Assets/Materials/PS1PostProcess.mat\n\n" +
                "── Next steps ──────────────────────────────────────────────────────\n" +
                "1. In Project Settings > Graphics, make sure your URP pipeline asset\n" +
                "   is a Universal Render Pipeline Asset (not Renderer2D for 3D scenes).\n\n" +
                "2. Create a 3D Universal Renderer:\n" +
                "   Project window → Create > Rendering > URP Universal Renderer\n" +
                "   Save it as Assets/Settings/UniversalRenderer3D.asset\n\n" +
                "3. In the URP pipeline asset, set Default Renderer to the new 3D renderer.\n\n" +
                "4. On the new renderer, click Add Renderer Feature → PS1RendererFeature.\n" +
                "   Assign Assets/Materials/PS1PostProcess.mat to its Material field.\n\n" +
                "5. Add TerminalController to a Canvas/GameObject in your game scene\n" +
                "   and point its UIDocument at Assets/UIToolkit/Terminal/TerminalView.uxml.\n" +
                "──────────────────────────────────────────────────────────────────────");
        }
    }
}
