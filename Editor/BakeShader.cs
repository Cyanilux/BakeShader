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
		private static int blit3DSliceProperty = Shader.PropertyToID("_Slice"); // Used for Texture3D and Flipbook baking
        private static GlobalKeyword bakeShaderKeyword = GlobalKeyword.Create("_BAKESHADER");

        // -----------------------------------------------------------

		#region EditorWindow

		public enum BakeType {
			Texture2D,
			Texture3D,
			MeshRenderer,
			Flipbook
		}

		private class BakeShaderWindow : EditorWindow {

			public Vector3Int res3D = new Vector3Int(128, 128, 128);
            public Vector3Int resFlipbook = new Vector3Int(256, 256, 32);
			public Vector2Int res2D = new Vector2Int(2048, 2048);
			public BakeType bakeType;
			public MeshRenderer meshRenderer;
			public Material material;
			public string path = "BakedTextures";

			private void OnGUI(){
				EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
				bakeType = (BakeType) EditorGUILayout.EnumPopup("Bake Type", bakeType);

				if (bakeType == BakeType.Texture3D){
					res3D = EditorGUILayout.Vector3IntField("Resolution", res3D);
				}else if (bakeType == BakeType.Flipbook){
					Vector2Int res = EditorGUILayout.Vector2IntField("Frame Resolution", new Vector2Int(resFlipbook.x, resFlipbook.y));
                    resFlipbook.x = res.x;
                    resFlipbook.y = res.y;
                    resFlipbook.z = EditorGUILayout.IntField("Frame Count", resFlipbook.z);
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
				if (bakeType == BakeType.Texture2D){
					BakeShader.Blit2D(material, res2D, path);
				}else if (bakeType == BakeType.Texture3D){
					BakeShader.Blit3D(material, res3D, path);
				}else if (bakeType == BakeType.MeshRenderer){
					BakeShader.Renderer(meshRenderer, res2D, path);
				}else if (bakeType == BakeType.Flipbook){
					BakeShader.Blit3D(material, resFlipbook, path, true);
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
        
		[MenuItem("CONTEXT/Material/Bake/Texture2D (png)")]
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

		[MenuItem("CONTEXT/Material/Bake/Texture3D")]
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

		[MenuItem("CONTEXT/Material/Bake/Flipbook")]
		private static void ContextMenu_BakeFlipbook(MenuCommand command) {
			Vector3Int resFlipbook = new Vector3Int(256, 256, 32);
			string path = "BakedTextures";
			if (EditorWindow.HasOpenInstances<BakeShaderWindow>()){
				BakeShaderWindow window = (BakeShaderWindow)EditorWindow.GetWindow(typeof(BakeShaderWindow));
				resFlipbook = window.resFlipbook;
				path = window.path;
			}
			Material material = (Material)command.context;
			Blit3D(material, resFlipbook, path, true);
		}

		#endregion

		#region Baking

		public static void Blit2D(Material material, Vector2Int resolution, string path){
			RenderTexture rt = RenderTexture.GetTemporary(resolution.x, resolution.y);

			// Blit
			Graphics.Blit(null, rt, material, 0);

			// Read & Save
			Texture2D tex2D = ReadPixels2D(rt);
			SavePNG(path, GetMaterialName(material) + "_BakedTexture", tex2D.EncodeToPNG());
			
			// Cleanup
			Object.DestroyImmediate(tex2D);
			RenderTexture.ReleaseTemporary(rt);
			RenderTexture.active = null;
		}

		public static void Blit3D(Material material, Vector3Int resolution, string path, bool saveAsFlipbook = false){
			int w = resolution.x;
			int h = resolution.y;
			int d = resolution.z;
			RenderTextureDescriptor desc = new RenderTextureDescriptor(w, h) {
				dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
				volumeDepth = d
			};
			RenderTexture rt = RenderTexture.GetTemporary(desc);

			// Blit
			for (int i = 0; i < d; i++) {
				material.SetFloat(blit3DSliceProperty, ((i + 0.5f) / d));
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

				if (saveAsFlipbook){
					// Save as Flipbook (PNG containing all slices/frames, vertically so I don't need to reorder array...)
					Texture2D tex = new Texture2D(w, h * d);
                    tex.SetPixels32(arr);
					SavePNG(path, GetMaterialName(material) + "_BakedFlipbook", tex.EncodeToPNG());
                }else{
					// Save as Texture3D
					Texture3D tex = new Texture3D(w, h, d, TextureFormat.RGBA32, 0);
					tex.SetPixels32(arr, 0);
                    SaveTexture3DAsset(path, GetMaterialName(material) + "_BakedTexture3D", tex);
                    RenderTexture.ReleaseTemporary(rt);
				}
				
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
			SavePNG(path, GetMaterialName(material) + "_BakedTexture", tex2D.EncodeToPNG());

			// Enable "Alpha Is Transparency"
			// (Unity should then stretch pixels in transparent areas, avoiding black seams around UV islands)
			TextureImporter texImporter = (TextureImporter) AssetImporter.GetAtPath("Assets/" + path + "/" + GetMaterialName(material) + "_BakedTexture.png");
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

			string pathName = path + "/" + name + ".png";
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

		private static void SaveTexture3DAsset(string path, string name, Texture3D tex){
			// Save Texture3D Asset
			Directory.CreateDirectory(Application.dataPath + "/" + path);
            tex.name = name;
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
		}

		private static string GetMaterialName(Material material){
            string name = material.name;
            if (name.StartsWith("Material/")){
                // embedded under graph
                name = name.Substring(9);
            }
            return name;
        }

		#endregion
    }

}