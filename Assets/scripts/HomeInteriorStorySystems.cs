using System;
using System.Collections;
using UnityEngine;

public enum HomeInteriorPhase
{
    Intro,
    Exploring,
    EnemyEvent,
    EnemyActive,
    ReadyToExit,
    Transitioning
}

public enum HomeInteriorInteractableKind
{
    Examine,
    Diary1,
    Diary2,
    Diary3,
    Key,
    Entrance
}

public sealed class HomeInteriorInteractable : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private HomeInteriorInteractableKind kind;
    [SerializeField, TextArea] private string[] dialogueLines;
    private HomeInteriorProgressManager progress;

    public void Configure(HomeInteriorProgressManager manager,
        HomeInteriorInteractableKind interactableKind, params string[] lines)
    {
        progress = manager;
        kind = interactableKind;
        dialogueLines = lines;
    }

    public bool CanInteract(FirstPersonController player)
    {
        return progress != null && progress.CanInteract(kind);
    }

    public void Interact(FirstPersonController player)
    {
        if (progress != null) progress.Interact(kind, dialogueLines);
    }
}

public sealed class HomeInteriorPlayerRoomTrigger : MonoBehaviour
{
    private HomeInteriorProgressManager progress;
    public void Configure(HomeInteriorProgressManager manager) => progress = manager;

    private void OnTriggerEnter(Collider other)
    {
        if (progress == null) return;
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player != null) progress.TryStartEnemyEvent();
    }
}

public sealed class HomeInteriorProgressManager : MonoBehaviour
{
    private FirstPersonController player;
    private ApartmentLoopControlLockManager control;
    private ApartmentLoopDialogueManager dialogue;
    private HomeInteriorEnemyController enemy;
    private SceneFadeTransition transition;
    private GameObject keyObject;
    private AudioSource storyAudio;
    private AudioClip doorClip;
    private Transform checkpoint;
    private bool[] diaries = new bool[3];
    private bool enemyEventStarted;
    private bool keyCollected;
    private bool transitioning;

    public HomeInteriorPhase Phase { get; private set; } = HomeInteriorPhase.Intro;
    public bool Diary1Read => diaries[0];
    public bool Diary2Read => diaries[1];
    public bool Diary3Read => diaries[2];
    public bool EnemyActive => enemy != null && enemy.IsActive;
    public bool KeyCollected => keyCollected;
    public bool ReadyToExit => keyCollected && !transitioning;

    public void Configure(FirstPersonController playerController,
        ApartmentLoopControlLockManager lockManager,
        ApartmentLoopDialogueManager dialogueManager,
        HomeInteriorEnemyController enemyController,
        SceneFadeTransition fadeTransition,
        GameObject key,
        AudioSource audioSource,
        AudioClip entranceDoorClip,
        Transform retryCheckpoint)
    {
        player = playerController;
        control = lockManager;
        dialogue = dialogueManager;
        enemy = enemyController;
        transition = fadeTransition;
        keyObject = key;
        storyAudio = audioSource;
        doorClip = entranceDoorClip;
        checkpoint = retryCheckpoint;
        if (keyObject != null) keyObject.SetActive(false);
        if (enemy != null) enemy.SetActive(false);
    }

    public IEnumerator Begin()
    {
        Phase = HomeInteriorPhase.Intro;
        dialogue.BeginDialogue(new[] { "ただいま", "............。" }, () =>
        {
            Phase = HomeInteriorPhase.Exploring;
        });
        yield break;
    }

    public bool CanInteract(HomeInteriorInteractableKind kind)
    {
        if (dialogue == null || dialogue.IsActive || transitioning) return false;
        return kind switch
        {
            HomeInteriorInteractableKind.Key =>
                enemyEventStarted && !keyCollected && keyObject != null && keyObject.activeInHierarchy,
            HomeInteriorInteractableKind.Entrance => ReadyToExit,
            _ => Phase is HomeInteriorPhase.Exploring or
                HomeInteriorPhase.EnemyActive or HomeInteriorPhase.ReadyToExit
        };
    }

    public void Interact(HomeInteriorInteractableKind kind, string[] lines)
    {
        if (!CanInteract(kind)) return;
        switch (kind)
        {
            case HomeInteriorInteractableKind.Diary1: ReadDiary(0, lines); break;
            case HomeInteriorInteractableKind.Diary2: ReadDiary(1, lines); break;
            case HomeInteriorInteractableKind.Diary3: ReadDiary(2, lines); break;
            case HomeInteriorInteractableKind.Key: CollectKey(); break;
            case HomeInteriorInteractableKind.Entrance: ExitHome(); break;
            default: dialogue.BeginDialogue(lines); break;
        }
    }

    private void ReadDiary(int index, string[] lines)
    {
        diaries[index] = true;
        dialogue.BeginCenteredDialogue(lines);
    }

    public void TryStartEnemyEvent()
    {
        if (enemyEventStarted || !Diary1Read || !Diary2Read || !Diary3Read ||
            Phase != HomeInteriorPhase.Exploring) return;
        enemyEventStarted = true;
        StartCoroutine(EnemyEventRoutine());
    }

    private IEnumerator EnemyEventRoutine()
    {
        Phase = HomeInteriorPhase.EnemyEvent;
        control.Acquire(ApartmentLoopLockReason.Cinematic);
        if (storyAudio != null && doorClip != null) storyAudio.PlayOneShot(doorClip);
        if (enemy != null) enemy.SetActive(true);
        if (keyObject != null) keyObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.35f);
        control.Release(ApartmentLoopLockReason.Cinematic);
        dialogue.BeginDialogue(new[] { "......！", "押し入れに隠れよう" }, () =>
        {
            Phase = HomeInteriorPhase.EnemyActive;
        });
    }

    private void CollectKey()
    {
        if (keyCollected) return;
        keyCollected = true;
        if (keyObject != null) keyObject.SetActive(false);
        Phase = HomeInteriorPhase.ReadyToExit;
    }

    private void ExitHome()
    {
        if (!ReadyToExit) return;
        transitioning = true;
        Phase = HomeInteriorPhase.Transitioning;
        transition.Begin("ChaseMap", control);
    }

    public void ContinueAfterGameOver()
    {
        if (checkpoint != null)
            player.RestorePose(checkpoint.position, checkpoint.rotation);
        player.SetHidden(false);
        enemy?.ResetToSpawn();
    }
}
