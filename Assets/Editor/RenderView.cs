using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class RenderView : EditorWindow
{
    public Texture2D outputTexture;

    void OnFocus()
    {
        titleContent = new GUIContent("Render View");
    }

    void OnGUI()
    {
        if (outputTexture != null)
        {
            EditorGUI.DrawPreviewTexture(new Rect(10, 10, position.width - 20, position.height - 20), outputTexture, null, ScaleMode.ScaleToFit);
        }
    }
}
