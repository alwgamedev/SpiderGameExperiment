using UnityEngine;

public class CrystalSpike : MonoBehaviour
{
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] Vector2 maxStretch;//we could also randomize
    [SerializeField] float maxHighlight;
    [SerializeField] float stretchRate;
    [SerializeField] float shrinkRateMin;
    [SerializeField] float shrinkRateMax;
    [SerializeField] float restRate;

    Material material;
    readonly int stretchProperty = Shader.PropertyToID("_Stretch");
    readonly int highlightProperty = Shader.PropertyToID("_EdgeHighlight");
    float timer;
    float restHighlight;
    float startHighlight;
    Vector2 startStretch;
    Vector4 animation0;//(x, y) = stretch goal, z = highlightGoal, w = timerMax
    Vector4 animation1;
    Vector4 animation2;

    private void Start()
    {
        material = new Material(meshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = material;
        restHighlight = material.GetFloat(highlightProperty);
    }

    private void OnDestroy()
    {
        Destroy(material);
    }

    private void Update()
    {
        var timerMax = animation0.w;
        if (timer < timerMax)
        {
            timer += Time.deltaTime;
            float t = timer / timerMax;
            var highlight = Mathf.Lerp(startHighlight, animation0.z, t);
            var stretch = Vector2.Lerp(startStretch, animation0, t);
            material.SetFloat(highlightProperty, highlight);
            material.SetVector(stretchProperty, stretch);

            if (timer >= timerMax && animation1.w > 0)
            {
                animation0 = animation1;
                animation1 = animation2;
                animation2 = Vector3.zero;

                timer = 0;
                startHighlight = material.GetFloat(highlightProperty);
                startStretch = material.GetVector(stretchProperty);
            }
            //or if animation1.z == 0 (no next animation), we don't reset timer, 
            //so we just sit idly until we're given another animation
        }
    }

    public bool Attack()
    {
        if (timer < animation0.w)
        {
            //we're already attacking 
            //(use the length of the animation cycle as a cooldown / prevent multiple colliders on same entity from re-triggering)
            return false;
        }
        Vector2 curStretch = material.GetVector(stretchProperty);
        animation0 = StretchAnimation(curStretch, maxStretch, maxHighlight, stretchRate);
        animation1 = new(maxStretch.x, maxStretch.y, maxHighlight, 1 / restRate);
        var shrinkRate = MathTools.RandomFloat(shrinkRateMin, shrinkRateMax);
        animation2 = StretchAnimation(maxStretch, Vector2.one, restHighlight, shrinkRate);

        timer = 0;
        startHighlight = material.GetFloat(highlightProperty);
        startStretch = curStretch;
        return true;
    }

    Vector4 StretchAnimation(Vector2 startStretch, Vector2 endStretch, float highlightGoal, float stretchRate)
    {
        var timerMax = Mathf.Max(Mathf.Abs(endStretch.x - startStretch.x), Mathf.Abs(endStretch.y - startStretch.y)) / stretchRate;
        timerMax = Mathf.Max(timerMax, 0.1f);
        //^timerMax 0 is used to indicate no animation, so clamp timerMax above 0
        return new(endStretch.x, endStretch.y, highlightGoal, timerMax);
    }
}