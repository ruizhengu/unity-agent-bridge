using UnityEngine;

public class TestError : MonoBehaviour
{
    void Start()
    {
        // Missing semicolon creates a syntax error
        Debug.Log("Broken")
    }
}
