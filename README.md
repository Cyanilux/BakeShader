## BakeShader

![Bake](https://user-images.githubusercontent.com/69320946/187734767-1b574014-9d53-4f83-86e3-7f06f7ad2188.gif)

- Adds "Bake Shader" window (under Window/Cyanilux/BakeShader)
	- Includes modes : Blit2D (png), Blit3D (Texture3D asset), MeshRenderer
- Also adds "Bake" options to the "..." context menus on Material asset and MeshRenderer component
	- (If BakeShader window is open, this will use resolution & path settings set there, otherwise uses defaults)
- Should work in any render pipeline!

### Setup:
- Install via Package Manager → Add package via git URL : 
  - `https://github.com/Cyanilux/BakeShader.git`
- Alternatively, download and put the folder in your Assets

### Notes : 
- Blit2D : Shader is blit (basically draws a quad) to a Texture2D
    - Result saved as .png
- Blit3D : Shader is blit to a Texture3D in slices
    - Use "_SliceDepth" Float property in shader. This ranges from 0 to 1 through the depth of the Texture3D
    - Result saved as Texture3D asset
- MeshRenderer : Renderer is drawn to Texture2D
    - Will bake using first Material only
    - Scene View must be open for Renderer baking to occur
    - For correct results, shader should be **unlit** and **output using UV coordinates instead of vertices** - to handle this :
        - For graphs, use the provided `Bake Shader Vertex Pos` subgraph in the **Position** port on the **Vertex** stack. Also use **Render Face : Both** in graph settings.
            - Note that using the `Position` node in **Fragment** stage will now produce different results, as this changes the vertex positions. In v12+ we can avoid this by using a **Custom Interpolator** to pass the unmodified vertex pos through, if required. See : https://www.cyanilux.com/tutorials/intro-to-shader-graph/#custom-interpolators
        - For cg/hlsl, this should work (though some variables may need renaming). The pass should also specify `Cull Off` :

```
#pragma multi_compile _ _BAKESHADER

// in vertex function :
#ifdef _BAKESHADER
    float2 remappedUV = IN.uv.xy * 2 - 1;
    float3 bakeShaderPos = float3(remappedUV.x, remappedUV.y, 0);
#else
    float3 bakeShaderPos = IN.vertex;
#endif

OUT.vertex = UnityObjectToClipPos(bakeShaderPos); // Built-in RP
OUT.positionCS = TransformObjectToHClip(bakeShaderPos); // URP
```
