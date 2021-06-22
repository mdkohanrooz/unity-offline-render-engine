using UnityEngine;
using System.Collections;

public class Test : MonoBehaviour
{
    void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] tris = mesh.triangles;

        tris[0] = tris[5];

        mesh.triangles = tris;
        mesh.vertices = vertices;
    }
    void Update()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
    }
}