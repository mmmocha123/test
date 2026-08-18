using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ApartmentLoopStoryPhase
{
    Intro,
    CheckingHomeDoor,
    SearchingForKey,
    CrowEvent,
    ReturningHome,
    CheckingReturnedHomeDoor,
    AscendingToWrongFloor,
    WrongFloorRealization,
    ThreatIntroduction,
    ThreatActive,
    PostFirstHide,
    HomeSearch,
    HomeRediscovered,
    TransitionToHome
}

public sealed class ApartmentLoopProgressManager : MonoBehaviour
{
    private FloorLoopManager floorLoop; private FirstPersonController player; private PlayerInteraction interaction; private FlashlightController flashlight; private ApartmentLoopControlLockManager control; private ApartmentLoopDialogueManager dialogue; private ApartmentLoopLightingController lighting; private ApartmentLoopCameraDirector cameraDirector; private FacePeekEventController face; private EnemyWaypointGraph graph; private InvisibleEnemyController enemy; private ApartmentLoopGameOverManager gameOver; private ApartmentLoopStairAccessController stairs; private SceneFadeTransition transition;
    private readonly HashSet<int> visited = new(); private ApartmentLoopCheckpoint checkpoint; private bool firstHideSuccess; private bool facePlayed; private bool waitingForSuccessfulExit; private int returnedHomeLogicalFloor;
    public ApartmentLoopStoryPhase Phase { get; private set; }

    public void Configure(FloorLoopManager fl, FirstPersonController p, PlayerInteraction pi, FlashlightController flash, ApartmentLoopControlLockManager c, ApartmentLoopDialogueManager d, ApartmentLoopLightingController l, ApartmentLoopCameraDirector cd, FacePeekEventController fp, EnemyWaypointGraph g, InvisibleEnemyController e, ApartmentLoopGameOverManager go, ApartmentLoopStairAccessController sa, SceneFadeTransition st)
    {
        floorLoop=fl;player=p;interaction=pi;flashlight=flash;control=c;dialogue=d;lighting=l;cameraDirector=cd;face=fp;graph=g;enemy=e;gameOver=go;stairs=sa;transition=st;
        floorLoop.FloorChanged += OnFloorChanged; interaction.HiddenStateChanged += OnHiddenChanged; enemy.FirstHideSurvived += OnFirstHideSurvived;
    }

    public IEnumerator BeginAfterSceneReady()
    {
        yield return null;
        foreach(FloorRuntimeData floor in floorLoop.FloorsByHeight){floor.ConfigureRuntime();floor.Door.ConfigureRuntime();floor.Door.StoryInteracted+=OnDoorInteracted;floor.CrowKeyEvent.KeyCollected+=OnKeyCollected;}
        graph.RebuildCrossFloorLinks(); player.SetSprintEnabled(false); flashlight.SetLightOn(false); Phase=ApartmentLoopStoryPhase.Intro; ApplyPhase(); dialogue.BeginDialogue(ApartmentLoopDialogueLines.Intro,()=>{Phase=ApartmentLoopStoryPhase.CheckingHomeDoor;ApplyPhase();});
    }

    private void ApplyPhase()
    {
        stairs.SilentBlock=Phase>=ApartmentLoopStoryPhase.HomeRediscovered;
        stairs.Mode=GetStairMode();
        bool hideAvailable=Phase>=ApartmentLoopStoryPhase.ThreatActive&&Phase<ApartmentLoopStoryPhase.HomeRediscovered;
        foreach(var f in floorLoop.FloorsByHeight){f.Door.SetRole(hideAvailable?ApartmentLoopDoorRole.HideDoor:ApartmentLoopDoorRole.UnavailableHideDoor);f.Door.SetHighlight(false);}
        if(Phase is ApartmentLoopStoryPhase.Intro or ApartmentLoopStoryPhase.CheckingHomeDoor){var f=floorLoop.GetCurrentFloor();f.Door.SetRole(ApartmentLoopDoorRole.HomeDoorLocked);f.Door.SetHighlight(true);}
        else if(Phase==ApartmentLoopStoryPhase.CheckingReturnedHomeDoor){var f=floorLoop.FloorsByHeight.FirstOrDefault(x=>x.LogicalFloorIndex==returnedHomeLogicalFloor);if(f!=null){f.Door.SetRole(ApartmentLoopDoorRole.ReturnedHomeDoor);f.Door.SetHighlight(true);}}
    }

    private StairAccessMode GetStairMode()
    {
        if(Phase is ApartmentLoopStoryPhase.Intro or ApartmentLoopStoryPhase.CheckingHomeDoor)return StairAccessMode.None;
        if(Phase is ApartmentLoopStoryPhase.SearchingForKey or ApartmentLoopStoryPhase.CrowEvent)return StairAccessMode.DownOnly;
        if(Phase is ApartmentLoopStoryPhase.ReturningHome or ApartmentLoopStoryPhase.AscendingToWrongFloor)return StairAccessMode.UpOnly;
        if(Phase==ApartmentLoopStoryPhase.CheckingReturnedHomeDoor)return floorLoop.CurrentLogicalFloorIndex<returnedHomeLogicalFloor?StairAccessMode.UpOnly:StairAccessMode.DownOnly;
        if(Phase is ApartmentLoopStoryPhase.HomeRediscovered or ApartmentLoopStoryPhase.TransitionToHome)return StairAccessMode.None;
        return StairAccessMode.Both;
    }

    private void ResetVisits(){visited.Clear();visited.Add(floorLoop.CurrentLogicalFloorIndex);}
    private int AddVisit(int logical){visited.Add(logical);return visited.Count-1;}
    private void OnFloorChanged(int previous,int current,FloorMoveDirection direction)
    {
        int count=AddVisit(current);
        if(Phase==ApartmentLoopStoryPhase.SearchingForKey && direction==FloorMoveDirection.Down && count>=2) StartCoroutine(BeginCrowEvent());
        else if(Phase==ApartmentLoopStoryPhase.ReturningHome && direction==FloorMoveDirection.Up && count>=2){returnedHomeLogicalFloor=current;Phase=ApartmentLoopStoryPhase.CheckingReturnedHomeDoor;ResetVisits();ApplyPhase();}
        else if(Phase==ApartmentLoopStoryPhase.CheckingReturnedHomeDoor){ApplyPhase();}
        else if(Phase==ApartmentLoopStoryPhase.AscendingToWrongFloor && direction==FloorMoveDirection.Up && count>=1){Phase=ApartmentLoopStoryPhase.WrongFloorRealization;ApplyPhase();dialogue.BeginDialogue(ApartmentLoopDialogueLines.RepeatedFloor,()=>{ResetVisits();ApplyPhase();});}
        else if(Phase==ApartmentLoopStoryPhase.WrongFloorRealization && count>=1) BeginThreatIntroduction();
        else if(Phase==ApartmentLoopStoryPhase.HomeSearch){if(count==2&&!facePlayed){facePlayed=true;Transform target=direction==FloorMoveDirection.Up?floorLoop.GetCurrentFloor().UpperFace:floorLoop.GetCurrentFloor().LowerFace;StartCoroutine(face.Play(target,null));}if(count>=5) BeginHomeRediscovered();}
    }

    private void OnDoorInteracted(ApartmentLoopDoorRoleController door)
    {
        if(door.Role==ApartmentLoopDoorRole.HomeDoorLocked){door.SetHighlight(false);Phase=ApartmentLoopStoryPhase.SearchingForKey;ResetVisits();ApplyPhase();dialogue.BeginDialogue(ApartmentLoopDialogueLines.MissingKey);}
        else if(door.Role==ApartmentLoopDoorRole.ReturnedHomeDoor){door.SetHighlight(false);Phase=ApartmentLoopStoryPhase.AscendingToWrongFloor;ResetVisits();ApplyPhase();dialogue.BeginDialogue(ApartmentLoopDialogueLines.WrongHomeFloor);}
        else if(door.Role==ApartmentLoopDoorRole.FinalHomeDoor){Phase=ApartmentLoopStoryPhase.TransitionToHome;ApplyPhase();transition.Begin("HomeInterior",control);}
    }

    private IEnumerator BeginCrowEvent()
    {
        Phase=ApartmentLoopStoryPhase.CrowEvent;
        ApplyPhase();
        control.Acquire(ApartmentLoopLockReason.Cinematic);
        StartCoroutine(ReleaseCrowCinematicFailsafe());

        try
        {
            FloorRuntimeData floor = floorLoop.GetCurrentFloor();
            if (floor == null) yield break;

            yield return lighting.PlayFlicker(floor, null);

            Light left = floor.LeftmostCorridorLight;

            if (left != null)
            {
                yield return cameraDirector.LookAt(left.transform);
            }

            if (floor.CrowKeyEvent != null)
            {
                floor.CrowKeyEvent.ActivateEvent();
            }
        }
        finally
        {
            control.Release(ApartmentLoopLockReason.Cinematic);
        }
    }

    private IEnumerator ReleaseCrowCinematicFailsafe()
    {
        yield return new WaitForSecondsRealtime(6f);

        if (Phase == ApartmentLoopStoryPhase.CrowEvent)
        {
            control.Release(ApartmentLoopLockReason.Cinematic);
        }
    }
    private void OnKeyCollected(){lighting.SetBlackout(true);Phase=ApartmentLoopStoryPhase.ReturningHome;ResetVisits();ApplyPhase();dialogue.BeginDialogue(ApartmentLoopDialogueLines.FoundKey);}
    private void BeginThreatIntroduction(){Phase=ApartmentLoopStoryPhase.ThreatIntroduction;checkpoint=new ApartmentLoopCheckpoint{playerPosition=player.transform.position,playerRotation=player.transform.rotation,playerLookPitch=player.LookPitch,floors=floorLoop.CaptureState()};ApplyPhase();dialogue.BeginDialogue(ApartmentLoopDialogueLines.Threat,ActivateEnemy);}
    private void ActivateEnemy(){Phase=ApartmentLoopStoryPhase.ThreatActive;ApplyPhase();ApplyThreatHighlight(true);FloorRuntimeData spawnFloor=floorLoop.FloorsByHeight.First();enemy.ActivateAt(spawnFloor.EnemySpawnPoint);}
    private void ApplyThreatHighlight(bool value){foreach(var f in floorLoop.FloorsByHeight)f.Door.SetHighlight(value);}
    private void OnFirstHideSurvived(){if(firstHideSuccess)return;firstHideSuccess=true;waitingForSuccessfulExit=true;ApplyThreatHighlight(false);}
    private void OnHiddenChanged(bool hidden){if(!hidden&&waitingForSuccessfulExit){waitingForSuccessfulExit=false;Phase=ApartmentLoopStoryPhase.PostFirstHide;dialogue.BeginDialogue(ApartmentLoopDialogueLines.FirstHide,()=>{Phase=ApartmentLoopStoryPhase.HomeSearch;facePlayed=false;ResetVisits();ApplyPhase();});}}
    private void BeginHomeRediscovered(){Phase=ApartmentLoopStoryPhase.HomeRediscovered;ApplyPhase();control.Acquire(ApartmentLoopLockReason.Cinematic);enemy.MakeSafe();FloorRuntimeData floor=floorLoop.GetCurrentFloor();floor.Door.SetRole(ApartmentLoopDoorRole.FinalHomeDoor);floor.Door.SetHighlight(true);StartCoroutine(HomeSequence(floor));}
    private IEnumerator HomeSequence(FloorRuntimeData floor){yield return cameraDirector.LookAt(floor.Door.transform);control.Release(ApartmentLoopLockReason.Cinematic);dialogue.BeginDialogue(ApartmentLoopDialogueLines.HomeRediscovered);}

    public void ContinueFromCheckpoint()
    {
        if(checkpoint==null)return;control.Acquire(ApartmentLoopLockReason.Reset);enemy.ResetInactive();floorLoop.RestoreState(checkpoint.floors);player.RestorePose(checkpoint.playerPosition,checkpoint.playerRotation);player.RestoreLookPitch(checkpoint.playerLookPitch);lighting.SetBlackout(true);flashlight.SetLightOn(false);firstHideSuccess=false;waitingForSuccessfulExit=false;facePlayed=false;foreach(var f in floorLoop.FloorsByHeight){f.CrowKeyEvent.ResetCompleted();f.UpperFace.gameObject.SetActive(false);f.LowerFace.gameObject.SetActive(false);}Phase=ApartmentLoopStoryPhase.ThreatIntroduction;ApplyPhase();graph.RebuildCrossFloorLinks();control.Release(ApartmentLoopLockReason.Reset);dialogue.BeginDialogue(ApartmentLoopDialogueLines.Threat,ActivateEnemy);
    }
}
