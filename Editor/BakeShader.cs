using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Unity.Collections;

/*
@Cyanilux

- Adds "Bake Shader" window (under Window/Cyanilux/BakeShader)
	- Includes modes : Blit2D (png), Blit3D (Texture3D asset), MeshRenderer
- Also adds "Bake" options to the "..." context menus on Material asset and MeshRenderer component
	- (If BakeShader window is open, this will use resolution & path settings set there, otherwise uses defaults)
- Should work in any render pipeline!

For usage instructions, see README file
*/

namespace Cyan {
	
    public class BakeShader {

        private static int delayBaking = 1;     // (in seconds)
		private static int blit3DSliceOffsetProperty = Shader.PropertyToID("_SliceDepth");
        private static GlobalKeyword bakeShaderKeyword = GlobalKeyword.Create("_BAKESHADER");

        // -----------------------------------------------------------

		#region EditorWindow

		public enum BakeType { Blit2D, Blit3D, MeshRenderer }

		private class BakeShaderWindow : EditorWindow {

			public Vector3Int res3D = new Vector3Int(128, 128, 128);
			public Vector2Int res2D = new Vector2Int(2048, 2048);
			public BakeType bakeType;
			public MeshRenderer meshRenderer;
			public Material material;
			public string path = "BakedTextures";

			private void OnGUI(){
				EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
				bakeType = (BakeType) EditorGUILayout.EnumPopup("Bake Type", bakeType);

				if (bakeType == BakeType.Blit3D){
					res3D = EditorGUILayout.Vector3IntField("Resolution", res3D);
				}else{
					res2D = EditorGUILayout.Vector2IntField("Resolution", res2D);
				}
				if (bakeType == BakeType.MeshRenderer){
					meshRenderer = (MeshRenderer) EditorGUILayout.ObjectField("Mesh Renderer", meshRenderer, typeof(MeshRenderer), true);
				}else{
					material = (Material) EditorGUILayout.ObjectField("Material", material, typeof(Material), true);
				}

				EditorGUILayout.Space();
				path = EditorGUILayout.TextField("Path (Assets/)", path);
				EditorGUILayout.Space();

				if (GUILayout.Button("Bake")){
					Bake();
				}
			}

			public void Bake(){
				if (bakeType == BakeType.Blit2D){
					BakeShader.Blit2D(material, res2D, path);
				}else if (bakeType == BakeType.Blit3D){
					BakeShader.Blit3D(material, res3D, path);
				}else if (bakeType == BakeType.MeshRenderer){
					BakeShader.Renderer(meshRenderer, res2D, path);
				}
			}

		}

		[MenuItem("Window/Cyanilux/Bake Shader")]
        private static void ShowWindow(MenuCommand command) {
			BakeShaderWindow window = (BakeShaderWindow)EditorWindow.GetWindow(typeof(BakeShaderWindow));
			window.titleContent = new GUIContent("Bake Shader");
			window.Show();
		}

		#endregion

        #region ContextMenus

        [MenuItem("CONTEXT/MeshRenderer/Bake/Material To Texture (using Mesh UV)")]
        private static void ContextMenu_Bake(MenuCommand command) {
			Vector2Int res2D = new Vector2Int(2048, 2048);
			string path = "BakedTextures";
			if (EditorWindow.HasOpenInstances<BakeShaderWindow>()){
				BakeShaderWindow window = (BakeShaderWindow)EditorWindow.GetWindow(typeof(BakeShaderWindow));
				res2D = window.res2D;
				path = window.path;
			}
			MeshRenderer meshRenderer = (MeshRenderer)command.context;
			Renderer(meshRenderer, res2D, path);
        }
        
		[MenuItem("CONTEXT/Material/Bake/Blit to Texture2D (png)")]
		private static void ContextMenu_Blit2D(MenuCommand command) {
			Vector2Int res2D = new Vector2Int(2048, 2048);
			string path = "BakedTextures";
			if (EditorWindow.HasOpenInstances<BakeShaderWindow>()){
				BakeShaderWindow window = (BakeShaderWindow)EditorWindow.GetWindow(typeof(BakeShaderWindow));
				res2D = window.res2D;
				path = window.path;
			}
			Material material = (Material)command.context;
			Blit2D(material, res2D, path);
		}

		[MenuItem("CONTEXT/Material/Bake/Blit to Texture3D")]
		private static void ContextMenu_Bake3D(MenuCommand command) {
			Vector3Int res3D = new Vector3Int(128, 128, 128);
			string path = "BakedTextures";
			if (EditorWindow.HasOpenInstances<BakeShaderWindow>()){
				BakeShaderWindow window = (BakeShaderWindow)EditorWindow.GetWindow(typeof(BakeShaderWindow));
				res3D = window.res3D;
				path = window.path;
			}
			Material material = (Material)command.context;
			Blit3D(material, res3D, path);
		}

		#endregion

		#region Baking

		public static void Blit2D(Material material, Vector2Int resolution, string path){
			RenderTexture rt = RenderTexture.GetTemporary(resolution.x, resolution.y);

			// Blit
			Graphics.Blit(null, rt, material, 0);

			// Read & Save
			Texture2D tex2D = ReadPixels2D(rt);
			SavePNG(path, material.name + "_BakedTexture.png", tex2D.EncodeToPNG());
			
			// Cleanup
			Object.DestroyImmediate(tex2D);
			RenderTexture.ReleaseTemporary(rt);
			RenderTexture.active = null;
		}

		public static void Blit3D(Material material, Vector3Int resolution, string path){
			int w = resolution.x;
			int h = resolution.y;
			int d = resolution.z;
			RenderTextureDescriptor desc = new RenderTextureDescriptor(w, d) {
				dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
				volumeDepth = d
			};
			RenderTexture rt = RenderTexture.GetTemporary(desc);

			// Blit
			for (int i = 0; i < d; i++) {
				material.SetFloat(blit3DSliceOffsetProperty, ((i + 0.5f) / d));
				Graphics.Blit(null, rt, material, 0, i);
			}

			// Readback
			AsyncGPUReadback.Request(rt, 0, 0, w, 0, h, 0, d, (AsyncGPUReadbackRequest rq) => {
				Color32[] arr = new Color32[w * h * d];
				int size = w * h;
				for (int i = 0; i < d; i++) {
					NativeArray<Color32> data = rq.GetData<Color32>(i);
					NativeArray<Color32>.Copy(data, 0, arr, i * size, size);
				}

				string name = material.name + "_BakedTexture3D";
				Texture3D tex = new Texture3D(w, h, d, TextureFormat.RGBA32, 0);
				tex.name = name;
				tex.SetPixels32(arr, 0);
				//tex.Apply();

				// Save Texture3D Asset
				Directory.CreateDirectory(Application.dataPath + "/" + path);
				string assetPath = "Assets/" + path + "/" + name + ".asset";
				Object existingAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
				if (existingAsset != null) {
					if (!EditorUtility.DisplayDialog("BakeShader", 
						"Warning : Asset at path '"+assetPath+"' already exists, override it?", 
						"Override!", "Cancel!")) {
						return;
					}
				}

				if (existingAsset == null) {
					AssetDatabase.CreateAsset(tex, assetPath);
				} else {
					EditorUtility.CopySerialized(tex, existingAsset);
				}
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				RenderTexture.ReleaseTemporary(rt);
			});
		}

		private static float initTime;
		private static MeshRenderer renderer;
		private static Vector2Int resolution;
		private static string path;

		public static void Renderer(MeshRenderer renderer, Vector2Int resolution, string path){
			initTime = Time.realtimeSinceStartup;
			BakeShader.renderer = renderer;
			BakeShader.resolution = resolution;
			BakeShader.path = path;
            
			// UnityEngine.Experimental.Rendering.ShaderWarmup.WarmupShader(renderer.sharedMaterial.shader, new ShaderWarmupSetup());
            // ShaderWarmup doesn't seem to help :\
            // Pretty hacky, but will enable keyword, add a delay... and just let Unity handle it!
			Shader.EnableKeyword(bakeShaderKeyword);
            EditorApplication.update += DelayedRendererBake;
		}
		
        private static void DelayedRendererBake() {
            if (Time.realtimeSinceStartup < initTime + delayBaking) return;

			// Unregister function, make sure we only bake once
            EditorApplication.update -= DelayedRendererBake;

			// Bake
			RenderTexture renderTarget = RenderTexture.GetTemporary(resolution.x, resolution.y);
            Mesh mesh = renderer.gameObject.GetComponent<MeshFilter>().sharedMesh;
            Material material = renderer.sharedMaterial;
            
            CommandBuffer cmd = new CommandBuffer();
			cmd.SetRenderTarget(renderTarget);
            cmd.EnableKeyword(bakeShaderKeyword);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.DrawRenderer(renderer, renderer.sharedMaterial, 0, 0);
            cmd.DisableKeyword(bakeShaderKeyword);
            Graphics.ExecuteCommandBuffer(cmd);

			// Read & Save
			Texture2D tex2D = ReadPixels2D(renderTarget);
			SavePNG(path, material.name + "_BakedTexture.png", tex2D.EncodeToPNG());

			// Enable "Alpha Is Transparency"
			// (Unity should then stretch pixels in transparent areas, avoiding black seams around UV islands)
			TextureImporter texImporter = (TextureImporter) AssetImporter.GetAtPath("Assets/" + path + "/" + material.name + "_BakedTexture.png");
			texImporter.alphaIsTransparency = true;
			EditorUtility.SetDirty(texImporter);
			texImporter.SaveAndReimport();

			// Cleanup
            Shader.DisableKeyword(bakeShaderKeyword);
			Object.DestroyImmediate(tex2D);
            RenderTexture.ReleaseTemporary(renderTarget);
        }

		private static Texture2D ReadPixels2D(RenderTexture rt){
			// Readback
			RenderTexture current = RenderTexture.active;
			RenderTexture.active = rt;
			Vector2Int resolution = new Vector2Int(rt.width, rt.height);
			Texture2D tex = new Texture2D(resolution.x, resolution.y);
			tex.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
			//tex.Apply();
			RenderTexture.active = current;
			return tex;
		}

		private static void SavePNG(string path, string name, byte[] bytes){
			Directory.CreateDirectory(Application.dataPath + "/" + path);

			string pathName = path + "/" + name;
			Object existingAsset = AssetDatabase.LoadAssetAtPath<Object>("Assets/" + pathName);
			if (existingAsset != null) {
				if (!EditorUtility.DisplayDialog("BakeShader", 
					"Warning : Asset at path 'Assets/"+pathName+"' already exists, override it?", 
					"Override!", "Cancel!")) {
					return;
				}
			}

			File.WriteAllBytes(Application.dataPath + "/" + pathName, bytes);
			AssetDatabase.Refresh();
		}

		#endregion
    }

}