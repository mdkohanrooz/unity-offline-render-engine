using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Threading;

public class RenderEngine : EditorWindow
{
    Camera RenderCamera;
    Light light;
    RenderEngineHelper renderHelper;
    float RenderProgress;
    IEnumerator rendererCoroutine;
    bool IsRendering;
    bool AsyncRendering;
    bool GIEnabled;
    bool UseSceneCamera;
    int GILightBounceCount = 1;
    float GILightBounceIndex = 1;
    float GIOpacity = 0.3f;
    float GIRayIntensityLossPerDistance = 0.1f;
    int GIRaysPerHitpoint;
    BoundingSphere SceneBoundingSphere;
    Transform helperTransform;

    // Add menu named "My Window" to the Window menu
    [MenuItem("Window/Render Engine")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        RenderEngine window = (RenderEngine)GetWindow(typeof(RenderEngine));
        window.titleContent = new GUIContent("Render Engine");
        window.Show();
    }
    void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Render Control", EditorStyles.boldLabel);
        if (IsRendering)
        {
            if (GUILayout.Button("Stop Rendering"))
                StopRender();
            RenderProgress = EditorGUILayout.Slider("Render Progress", RenderProgress, 0, 1);
        }
        else
        {
            if (GUILayout.Button("Render"))
                RenderIt();
        }
        AsyncRendering = EditorGUILayout.Toggle("Async Rendering (Slow)", AsyncRendering);

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Render Options", EditorStyles.boldLabel);
        if (!(UseSceneCamera = EditorGUILayout.Toggle("Use Scene Camera", UseSceneCamera)))
            RenderCamera = (Camera)EditorGUILayout.ObjectField("Render Camera", RenderCamera, typeof(Camera), true);

        GUILayout.Space(20);
        GIEnabled = EditorGUILayout.BeginToggleGroup("Global Illumination (GI)", GIEnabled);
        GILightBounceCount = (int)EditorGUILayout.Slider("Light Bounce Count",GILightBounceCount, 0, 64);
        GILightBounceIndex = EditorGUILayout.Slider("Light Bounce Index",GILightBounceIndex, 0, 10);
        GIOpacity = EditorGUILayout.Slider("Total Opacity", GIOpacity, 0, 1);
        GIRayIntensityLossPerDistance = EditorGUILayout.Slider("Ray Intensity Loss Per Distance", GIRayIntensityLossPerDistance, 0, 100);
        GIRaysPerHitpoint = (int)EditorGUILayout.Slider("Rays Per Hitpoint", GIRaysPerHitpoint, 1, 100000);
        EditorGUILayout.EndToggleGroup();

    }
    void Update()
    {
        if (rendererCoroutine != null)
            rendererCoroutine.MoveNext();
        if (IsRendering)
        {
            if (AsyncRendering)
                Repaint();
        }
    }
    public void RenderIt()
    {
        renderHelper = new GameObject("Render Helper").AddComponent<RenderEngineHelper>();
        if (!RenderCamera)
            RenderCamera = Camera.main;
        if (UseSceneCamera)
            RenderCamera = SceneView.lastActiveSceneView.camera;
        light = FindObjectOfType<Light>();
        renderHelper.StopAllCoroutines();
        renderHelper.StartCoroutine(rendererCoroutine = co_Render());
    }
    public void StopRender()
    {
        IsRendering = false;
    }

    IEnumerator co_Render()
    {
        // Initializing
        Debug.Log("Started");
        IsRendering = true;
        // Add Mesh Colliders to the objects of the scene and remove all other colliders
        Bounds bounds = new Bounds();
        GameObject[] GOs = FindObjectsOfType<GameObject>();
        foreach (GameObject go in GOs)
        {
            MeshRenderer rend = go.GetComponent<MeshRenderer>();
            if (rend == null)
                continue;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                DestroyImmediate(col, false);
            go.AddComponent<MeshCollider>();
            bounds.Encapsulate(rend.bounds);
        }
        bounds.Expand(1);
        SceneBoundingSphere = new BoundingSphere(bounds.center, (bounds.max - bounds.center).magnitude);

        // Prepare target texture
        Texture2D tex = new Texture2D(RenderCamera.pixelWidth, RenderCamera.pixelHeight);

        // A helper transform for transforming calculations
        helperTransform = new GameObject("Helper Transform").transform;

        long totalProgress = tex.width * tex.height;
        long currentProgress = 0;

        // Ray Tracing
        float distance;
        for (int x = 0; x < tex.width && IsRendering; x++)
        {
            for (int y = 0; y < tex.height && IsRendering; y++)
            {
                Ray ray = RenderCamera.ScreenPointToRay(new Vector3(x, y));
                Color col = GetPointColor(ray, 0, light.intensity, out distance);
                tex.SetPixel(x, y, col);
                currentProgress++;
                RenderProgress = (float)((double)currentProgress / totalProgress);
            }
            if (AsyncRendering)
                yield return new WaitUntil(TRUE);
        }

        // Apply changes to output texture
        tex.Apply();

        // Finalizing
        DestroyImmediate(helperTransform.gameObject);
        DestroyImmediate(renderHelper.gameObject);
        IsRendering = false;
        Debug.Log("Done");

        // Show texture
        RenderView outputWindow = GetWindow<RenderView>();
        outputWindow.outputTexture = tex;
        outputWindow.Show();

        yield return null;
    }
    bool TRUE()
    {
        return true;
    }
    
    Color GetPointColor(Ray ray, int deep, float intensity, out float distance, bool callFromGI = false)
    {
        Color finalColor;
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo) && deep <= GILightBounceCount)
        {
            distance = hitInfo.distance;
            // Object color
            MaterialOptions materialOptions = hitInfo.collider.GetComponent<MaterialOptions>();
            if (materialOptions == null)
                materialOptions = hitInfo.collider.gameObject.AddComponent<MaterialOptions>();
            Material objectMaterial = hitInfo.collider.GetComponent<Renderer>().sharedMaterial;
            // Calculate normal
            int i = hitInfo.triangleIndex * 3;
            Mesh objectMesh = hitInfo.collider.GetComponent<MeshCollider>().sharedMesh;
            Vector3[] normals = objectMesh.normals;
            Vector3 barycentericCoord = hitInfo.barycentricCoordinate;
            int[] tris = objectMesh.triangles;
            int v1 = tris[i];
            int v2 = tris[i + 1];
            int v3 = tris[i + 2];
            hitInfo.normal = (normals[v1] * barycentericCoord.x + normals[v2] * barycentericCoord.y + normals[v3] * barycentericCoord.z).normalized;
            hitInfo.normal = hitInfo.transform.TransformVector(hitInfo.normal);
            // Diffuse
            finalColor = objectMaterial.color;
            if (objectMaterial.mainTexture != null)
                finalColor *= (objectMaterial.mainTexture as Texture2D).GetPixel((int)(hitInfo.textureCoord.x * objectMaterial.mainTexture.width), (int)(hitInfo.textureCoord.y * objectMaterial.mainTexture.height));
            float colorAlpha = finalColor.a;
            // Direct light illumination
            finalColor *= light.color * light.intensity;
            float dotP = Vector3.Dot(light.transform.forward, hitInfo.normal) * -1;
            dotP = Mathf.Clamp(dotP, 0, 1);
            finalColor *= dotP;
            // Receive Shadow
            RaycastHit hitInfo_ShadowCheck;
            Ray shadowCheckRay = new Ray(hitInfo.point - (light.transform.forward * SceneBoundingSphere.radius * 2), light.transform.forward);
            Color ShadowColor = finalColor;
            do
            {
                if (Physics.Raycast(shadowCheckRay, out hitInfo_ShadowCheck))
                {
                    if (Vector3.Distance(hitInfo_ShadowCheck.point, hitInfo.point) > 0.001f)
                    {
                        Color shadowCasterColor = GetObjectRawColor(hitInfo_ShadowCheck);
                        //ShadowColor = Color.Lerp(ShadowColor, shadowCasterColor, 0.5f);
                        ShadowColor *= (1 - shadowCasterColor.a);
                        shadowCheckRay = new Ray(hitInfo_ShadowCheck.point + shadowCheckRay.direction * 0.001f, shadowCheckRay.direction);
                    }
                    else
                        break;
                }
                else
                {
                    break;
                }
            } while (true);
            finalColor = Color.Lerp(finalColor, ShadowColor, light.shadowStrength * (1-ShadowColor.a));
            // Return here when calling from GI
            if (callFromGI)
                return finalColor;
            // Transparency
            intensity *= (1 - colorAlpha);
            ray.direction = (-hitInfo.normal * (materialOptions.IndexOfRefraction - 1) + ray.direction);
            finalColor *= colorAlpha;
            finalColor += GetPointColor(new Ray(hitInfo.point + ray.direction * 0.001f, ray.direction), deep + 1, intensity, out distance) * (1 - colorAlpha);
            finalColor += (GIEnabled ? GetGIColor(ray) : Color.clear) * GIOpacity;
            return finalColor;
        }
        else
        {
            distance = float.PositiveInfinity;
            return RenderCamera.backgroundColor;
        }
    }
    Color GetGIColor(Ray ray)
    {
        RaycastHit hitInfo;
        if (!Physics.Raycast(ray, out hitInfo))
            return RenderCamera.backgroundColor;
        Color ColorSum = Color.clear;
        int hitCount = 1;
        // Backward tracing
        for (int GIn = 0; GIn < GIRaysPerHitpoint; GIn++)
        {
            helperTransform.up = hitInfo.normal;
            helperTransform.Rotate(Random.Range(-89, 89), 0, Random.Range(-89, 89), Space.Self);
            // Get object color
            float distance;
            Color colBackwardTrace = GetPointColor(new Ray(hitInfo.point, helperTransform.up), 0, light.intensity, out distance, true);
            float rayIntensity = light.intensity - GIRayIntensityLossPerDistance * distance;
            if (rayIntensity <= 0)
                continue;
            ColorSum += colBackwardTrace * rayIntensity;
            hitCount++;
        }
        ColorSum /= hitCount;
        return ColorSum * GIOpacity;
    }
    Color GetObjectRawColor(RaycastHit hitInfo)
    {
        // Object color
        Material objectMaterial = hitInfo.collider.GetComponent<Renderer>().sharedMaterial;
        Color finalColor = objectMaterial.color;
        if (objectMaterial.mainTexture != null)
            finalColor *= (objectMaterial.mainTexture as Texture2D).GetPixel((int)(hitInfo.textureCoord.x * objectMaterial.mainTexture.width), (int)(hitInfo.textureCoord.y * objectMaterial.mainTexture.height));
        return finalColor;
    }













    
}