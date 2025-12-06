using UnityEngine;

public class Geyser : MonoBehaviour
{
    GeyserMesh mesh;
    ParticleSystem particles;

    bool active;
    bool playerInContact;

    float particleP0;
    float particleV0;
    float particleG;
    bool erupting;
    float eruptTimer;
    float eruptTime;

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
        if (!active && Input.GetKey(KeyCode.P))
        {
            Erupt();
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            if (erupting)
            {
                UpdateEruption(Time.deltaTime);
            }
            HandleCollision(Time.deltaTime);
        }
    }

    private void HandleCollision(float dt)
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
        particleP0 = particles.transform.position.y - transform.position.y;
        particleV0 = particles.main.startSpeed.constantMin;
        particleG = particles.main.gravityModifier.constant * Physics2D.gravity.y;
        eruptTime = -particleV0 / particleG;
        eruptTimer = 0;
        particles.Play();
        active = true;
        erupting = true;
    }


    private void UpdateEruption(float dt)
    {
        eruptTimer += dt;
        if (eruptTimer > eruptTime)
        {
            eruptTimer = eruptTime;
            erupting = false;
        }
        mesh.SetHeight(ParticleHeight(eruptTimer));
    }

    private float ParticleHeight(float t)
    {
        return 0.5f * particleG * t * t + particleV0 * t + particleP0;
    }
}