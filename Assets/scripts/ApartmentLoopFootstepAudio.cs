using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ApartmentLoopFootstepAudio : MonoBehaviour
{
    private FirstPersonController playerController;
    private AudioSource audioSource;
    private float walkPitch;
    private float sprintPitch;
    private float crouchPitch;

    public void Configure(
        FirstPersonController controller,
        AudioClip clip,
        float volume,
        float walkingPitch = 1f,
        float runningPitch = 1.5f,
        float crouchingPitch = 0.8f)
    {
        playerController = controller;
        walkPitch = walkingPitch;
        sprintPitch = runningPitch;
        crouchPitch = crouchingPitch;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    private void Update()
    {
        if (audioSource == null ||
            audioSource.clip == null ||
            playerController == null ||
            !playerController.enabled ||
            Keyboard.current == null)
        {
            StopFootsteps();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool hasMovementInput =
            keyboard.wKey.isPressed ||
            keyboard.aKey.isPressed ||
            keyboard.sKey.isPressed ||
            keyboard.dKey.isPressed;

        if (!hasMovementInput)
        {
            StopFootsteps();
            return;
        }

        if (playerController.IsCrouching)
        {
            audioSource.pitch = crouchPitch;
        }
        else
        {
            audioSource.pitch = keyboard.leftShiftKey.isPressed
                ? sprintPitch
                : walkPitch;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void StopFootsteps()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void OnDisable()
    {
        StopFootsteps();
    }
}
