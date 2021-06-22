using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;


public class Utility
{
    Texture2D GIonTexture(Texture2D tex, Texture2D GI, float t)
    {
        if (tex.width != GI.width || tex.height != GI.height)
            throw new System.Exception("Textures must be at the same size.");

        Texture2D result = new Texture2D(tex.width, tex.height);

        for (int i = 0; i < tex.width; i++)
            for (int j = 0; j < tex.height; j++)
            {
                Color ColA = tex.GetPixel(i, j);
                Color ColB = GI.GetPixel(i, j);
                Color finalColor = ColA + ColB * t;
                result.SetPixel(i, j, finalColor);
            }

        result.Apply();
        return result;
    }
    Texture2D GaussianBlur(Texture2D a, int r, float alpha)
    {
        // g(x,y) = (1/2xPIxS^2)xe^-((x^2 + y^2)/(2xS^2))
        float[,] mask = new float[r, r];
        float maskSum = 0;
        int r2 = r / 2;
        for (int i = -r2; i <= r2; i++)
            for (int j = -r2; j <= r2; j++)
            {
                mask[i + r2, j + r2] = (1f / 2f * Mathf.PI * Mathf.Pow(alpha, 2)) * Mathf.Exp(-((Mathf.Pow(i, 2f) + Mathf.Pow(j, 2f)) / (2f * Mathf.Pow(alpha, 2f))));
                maskSum += mask[i + r2, j + r2];
            }

        Texture2D result = new Texture2D(a.width, a.height);

        for (int i = 0; i < a.width; i++)
        {
            for (int j = 0; j < a.height; j++)
            {
                float pixelSumR = 0;
                float pixelSumG = 0;
                float pixelSumB = 0;
                for (int x = 0; x < r; x++)
                    for (int y = 0; y < r; y++)
                    {
                        Color col = a.GetPixel(i + x - r2, j + y - r2) * mask[x, y];
                        pixelSumR += col.r;
                        pixelSumG += col.g;
                        pixelSumB += col.b;
                    }
                pixelSumR /= maskSum;
                pixelSumG /= maskSum;
                pixelSumB /= maskSum;

                result.SetPixel(i, j, new Color(pixelSumR, pixelSumG, pixelSumB));
            }
        }
        result.Apply();
        return result;
    }
}
