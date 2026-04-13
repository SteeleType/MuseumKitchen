using UnityEngine;

public class MapRenderer : MonoBehaviour
{
    [Header("Points of Origin")]
    public Vector2 pointA;
    public Vector2 pointB;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Add a LineRenderer component
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();

        // Set the material
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Set the color
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.green;

        // Set the width
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;

        // Set the number of vertices
        lineRenderer.positionCount = 3;

        // Set the positions of the vertices
        lineRenderer.SetPosition(0, new Vector3(0, 0, 0));
        lineRenderer.SetPosition(1, new Vector3(1, 1, 0));
        lineRenderer.SetPosition(2, new Vector3(2, 0, 0));
    }

    public void MoveMaps()
    {
        
    }

}
