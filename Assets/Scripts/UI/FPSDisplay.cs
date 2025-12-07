using TMPro;
using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp;
    [SerializeField] int frameFrequency;

    int frameCount;
    float timer;

    private void Update()
    {
        frameCount++;
        timer += Time.deltaTime;
        if (frameCount == frameFrequency)
        {
            tmp.text = $"FPS: {(int)(frameCount / timer)}";
            frameCount = 0;
            timer = 0;
        }
    }
}