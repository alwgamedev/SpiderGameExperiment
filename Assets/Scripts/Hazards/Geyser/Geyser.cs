using UnityEngine;

public class Geyser : MonoBehaviour
{
    GeyserMesh mesh;
    ParticleSystem particles;

    bool stateTransitionRequested;
    bool handlePlayerContact;
    bool playerInContact;

    GeyserState state;

    float particleP0;
    float particleV0;
    float particleG;
    float transitionTimer;
    float transitionTime;

    enum GeyserState
    {
        dormant, erupting, active, dissolving
    }

    private void Awake()
    {
        mesh = GetComponent<GeyserMesh>();
        particles = GetComponentInChildren<ParticleSystem>();
    }

    private void Start()
    {
        mesh.DoStart();
        mesh.SetHeight(0);
        EnableParticleCollision(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            stateTransitionRequested = true;
            //will be processed after any ongoing transition completes (so e.g. if in the middle of erupt, wait until that completes then dissolve)
        }

        switch (state)
        {
            case GeyserState.dormant:
                if (stateTransitionRequested)
                {
                    stateTransitionRequested = false;
                    Erupt();
                }
                break;
            case GeyserState.erupting:
                UpdateEruption(Time.deltaTime);
                break;
            case GeyserState.active:
                if (stateTransitionRequested)
                {
                    stateTransitionRequested = false;
                    Dissolve();
                }
                break;
            case GeyserState.dissolving:
                UpdateDissolve(Time.deltaTime);
                break;
        }
    }

    private void FixedUpdate()
    {
        //I guess doing it in fixedupdate so collider position will be accurate, really not that important tho
        if (handlePlayerContact)
        {
            HandlePlayerContact(Time.deltaTime);
        }
    }

    private void HandlePlayerContact(float dt)
    {
        var c = Spider.Player.TriggerCollider;
        if (mesh.Overlap(c.bounds))
        {
            if (!playerInContact)
            {
                EnableParticleCollision(true);
                playerInContact = true;
            }
            mesh.SetFade(c.bounds.min.y);
        }
        else
        {
            if (playerInContact)
            {
                playerInContact = false;
                EnableParticleCollision(false);
            }
            mesh.LerpResetFade(dt);
        }
    }

    private void EnableParticleCollision(bool val)
    {
        var c = particles.collision;
        c.enabled = val;
    }

    private void Erupt()
    {
        state = GeyserState.erupting;
        particleP0 = particles.transform.position.y - transform.position.y;
        particleV0 = particles.main.startSpeed.constantMin;
        particleG = particles.main.gravityModifier.constant * Physics2D.gravity.y;
        transitionTime = -particleV0 / particleG;
        transitionTimer = 0;
        handlePlayerContact = true;
        particles.Play();
    }


    private void UpdateEruption(float dt)
    {
        transitionTimer += dt;
        if (transitionTimer > transitionTime)
        {
            state = GeyserState.active;
        }
        mesh.SetHeight(ParticleHeight(transitionTimer));
    }

    private void Dissolve()
    {
        state = GeyserState.dissolving;
        mesh.SetHeight(0);
        handlePlayerContact = false;
        EnableParticleCollision(true);//since mesh bounds can't be used to detect contact anymore, just leave particle collision on until dissolve completes
        transitionTimer = 0;
        transitionTime = particles.main.startLifetime.constantMax;
        particles.Stop();
    }

    //only point of the dissolving state is so that collision stays active until last particles die;
    private void UpdateDissolve(float dt)
    {
        transitionTimer += dt;
        if (transitionTimer > transitionTime)
        {
            EnableParticleCollision(false);
            state = GeyserState.dormant;
        }
    }

    private float ParticleHeight(float t)
    {
        return 0.5f * particleG * t * t + particleV0 * t + particleP0;
    }
}