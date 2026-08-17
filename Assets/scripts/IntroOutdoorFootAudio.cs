using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class IntroOutdoorFootstepAudio : MonoBehaviour
{
    [Header("Player")]
    [SerializeField]
    private IntroOutdoorPlayerController playerController;

    [Header("Footstep")]
    [SerializeField]
    private AudioClip footstepClip;

    [SerializeField, Range(0f, 1f)]
    private float footstepVolume = 0.7f;

    [Header("Pitch")]
    [SerializeField]
    private float walkPitch = 1f;

    [SerializeField]
    private float sprintPitch = 1.15f;

    private CharacterController characterController;
    private AudioSource footstepAudioSource;

    private void Awake()
    {
        // PlayerのCharacterControllerを取得する
        // Get the Player's CharacterController
        characterController =
            GetComponent<CharacterController>();

        // PlayerのAudioSourceを取得する
        // Get the Player's AudioSource
        footstepAudioSource =
            GetComponent<AudioSource>();

        // AudioSourceへ足音を設定する
        // Assign the footstep clip to the AudioSource
        if (footstepAudioSource != null)
        {
            footstepAudioSource.clip =
                footstepClip;

            footstepAudioSource.loop = true;

            footstepAudioSource.playOnAwake = false;

            footstepAudioSource.volume =
                footstepVolume;
        }
    }

    private void Update()
    {
        // PlayerControllerが無効なら足音を止める
        // Stop footsteps while the PlayerController is disabled
        if (
            playerController == null ||
            !playerController.enabled
        )
        {
            StopFootsteps();
            return;
        }

        // CharacterControllerが無効なら足音を止める
        // Stop footsteps while the CharacterController is disabled
        if (
            characterController == null ||
            !characterController.enabled
        )
        {
            StopFootsteps();
            return;
        }

        // キーボードが存在しなければ足音を止める
        // Stop footsteps if no keyboard is available
        if (Keyboard.current == null)
        {
            StopFootsteps();
            return;
        }

        // WASDのどれかが押されているか確認する
        // Check whether any WASD movement key is held
        bool isMoving =
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.dKey.isPressed;

        // 移動入力がなければ足音を止める
        // Stop footsteps when there is no movement input
        if (!isMoving)
        {
            StopFootsteps();
            return;
        }

        // Shiftが押されているか確認する
        // Check whether the sprint key is held
        bool isSprinting =
            Keyboard.current.leftShiftKey.isPressed;

        // 歩行またはダッシュに応じて再生速度を変更する
        // Change playback speed for walking or sprinting
        footstepAudioSource.pitch =
            isSprinting
                ? sprintPitch
                : walkPitch;

        // 足音がまだ流れていなければ再生を開始する
        // Start the footstep loop if it is not already playing
        if (!footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Play();
        }
    }

    private void StopFootsteps()
    {
        // AudioSourceが存在しなければ処理しない
        // Do nothing if the AudioSource is missing
        if (footstepAudioSource == null)
        {
            return;
        }

        // 再生中の場合だけ停止する
        // Stop only while footsteps are playing
        if (footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Stop();
        }
    }

    private void OnDisable()
    {
        // スクリプトが無効化された場合も足音を止める
        // Stop footsteps when this component is disabled
        StopFootsteps();
    }
}