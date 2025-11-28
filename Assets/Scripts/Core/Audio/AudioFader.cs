using Unity.VisualScripting;
using UnityEngine;

public class AudioFader : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] float fadeTime;
    [SerializeField] float maxVol;

    bool shouldBePlaying;//state we're "lerping towards" (i.e. when false we're fading out or stopped)

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (audioSource.isPlaying)
        {
            UpdateFade();
        }
    }

    public void FadePlay()
    {
        shouldBePlaying = true;
        if (!audioSource.isPlaying)
        {
            audioSource.volume = 0f;
            audioSource.Play();
        }
    }

    public void FadeStop()
    {
        shouldBePlaying = false;
    }

    private void UpdateFade()
    {
        if (shouldBePlaying)
        {
            if (audioSource.volume < maxVol)
            {
                audioSource.volume = Mathf.Min(audioSource.volume + maxVol * Time.deltaTime / fadeTime, maxVol);
            }
        }
        else
        {
            if (audioSource.volume > 0)
            {
                audioSource.volume = Mathf.Max(audioSource.volume - maxVol * Time.deltaTime / fadeTime, 0);
            }
            if (!(audioSource.volume > 0))
            {
                audioSource.Stop();
            }
        }
    }
}
