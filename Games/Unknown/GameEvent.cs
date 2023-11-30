using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


/* GameEvent Setup and Usage

    GameEvents - UnityEvents that are played when the activation
    condition has been met. Multiple UnityEvents can be organized here,
    with a set time delay (in seconds) before the events are played.
   
    *************
    *Event Setup*
    *************
    
	1. Choose an "Activation Method"
		a. OnCall - manual call by using StartEvents()
		b. OnTriggerEnter/Exit - requires a trigger
		c. OnRoomEnter/Exit - requires a room
		d. OnCameraBlendStart/Stop - called when the camera starts
            transitioning or completely finishes transitioning
		e. OnPlayerVisible/Invisible - called when the camera loses
            direct line of sight with the player's neck area, or when
            the camera sees the player again
		
	2. Choose Activation Method "Condition" (if it pops up)
		a. IsPlayer - Gameobject is the same as the GameManage.Player object
		b. IsMonster - Gameobject has any of the scripts:
                SnailBehavior, Soundhound, Mimic_Behavior
		c. NameEquals - Check the name of the gameovbject that
                triggered the event
		d. TagEquals - Check the tag of the gameobject that triggered
                the event

	3. Set up "Events"
		a. Size = how many events will be run through for this GameEvent
		b. Delay = Seconds before this event is played (additive with
            all previous delays)
			i. So, a GameEvent set up as:
				1) Event 0 | Delay = 0.5  | "Flicker light on"
				2) Event 1 | Delay = 0.6  | "Flicker light off"
				3) Event 2 | Delay = 0    | "Activate next event"
				4) Event 3 | Delay = 0    | "Deactivate this object"
			ii. …will play out in this order:
				1) Wait 0.5 seconds
				2) Play event 0: "Flicker light on"
				3) Wait 0.6 seconds
				4) Play event 1: "Flicker light off"
				5) Wait 0 seconds
				6) Play event 2: "Activate next event"
				7) Wait 0 seconds
				8) Play event 3: "Deactivate this object"
		c. Event = UnityEvents to call when it is time

	4. Optionally set "Reprime after done" or "Loop after done"
		a. "Reprime After Done" - Readies the game event to be called after
                the last UnityEvent is called
        b. "Loop after done" - Immediately loop and play the events again
                after the last UnityEvent is called
 

    %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
    

    ************************
    *How to play the events*
    ************************
	1. Make sure the event is primed. Being primed means the event is
            ready to be played (when its conditions are correct).

        You can prime an event in then following ways:
		    a. Functions: (can be called from script or any GameEvent)
			    i. gameEvent.Prime()
			    ii. gameEvent.Unprime()
		    b. bool IsPrime = true, by script (doesnt appear in a
                UnityEvent for some reason)
            c. Checking "Loop After Done" or "Reprime After Done" in
                GameEvent inspector.
			
	2. The next time the conditions are correct (OnCall, TriggerEnter,
            CameraBlend, RoomEnter, ...) the GameEvents will play.


    Extra functions in case you want to manually start/stop the events
	    - StartEvents() - if primed, the events will play in order and with their delays
        - StopEvents() - if currently running the events, the events will be stopped

        - The "Events Reorderer" lets you drag the order of events around.
        - The "Events Reorderer" also contains a "Skip" checkbox for individual events
            to be skipped over when the GameEvent is played.

 */


public class GameEvent : MonoBehaviour
{
    [SerializeField] bool _eventDebugMessages;

    [SerializeField] Activate activationMethod;

    [SerializeField] Room room;

    //TriggerEnter/Exit Component
    [SerializeField] ActivateCheck _activationCondition;
    [SerializeField] string _activationConditionNameCheck;
    [SerializeField] string _activationConditionTagCheck;

    /// <summary>
    /// A GameEvent MUST be primed before it can be started.
    /// <br>You can prime it through some other script, or by using some other GameEvent, or by setting it to true in the inspector.</br>
    /// <br>Once primed, it will fire and immediately unprime itself.</br>
    /// </summary>
    [Space(7)]
    public bool IsPrimed = true;

    public bool IsRunning
    {
        get { return (_runningEvents != null); }
    }


    /// <summary>
    /// <para>
    /// List of UnityEvents that run in sequential order, each with an additive delay
    /// before moving onto the next one.
    /// </para>
    /// 
    /// <para>
    /// The events can be stopped mid-run by disabling its GameObject, or if StartEvents()
    /// gets called while the events are running.
    /// </para>
    /// 
    /// <para>
    /// In the Inspector, UnityEvents will only work with public variables or functions that are:
    /// <br>1. public</br>
    /// <br>2. and have a void return type</br>
    /// <br>3. and have either 0 or 1 arguments that are the primitive type or
    ///     are a Unity Object/ScriptableObject</br>
    /// </para>
    /// 
    /// <para>**DO NOT** rename this variable, ever.</para>
    /// </summary>
    [Space(7)]
    [SerializeField]
    EventsWithDelay[] _events;     //do NOT rename this ever.

    //Only either "afterDoneReprime" or "afterDoneLoop" can be selected in the inspector.
    [SerializeField]
    bool afterDoneReprime;          //if you want the game event to immediately reprime itself when it finishes

    [SerializeField]
    bool afterDoneLoop;             //if you want the game event to loop when it finishes. Reprimes itself automatically.

    private Coroutine _runningEvents;


    private void Awake()
    {
        //Warn if one of the UnityEvents are left empty in inspector
        for(int i = 0; i < _events.Length; i++)
        {
            int eventICount = _events[i].Events.GetPersistentEventCount();

            if(eventICount <= 0)
                Debug.LogWarning($"Game event at idx {i} has no events assigned.", this.gameObject);
        }

        RoomErrorCheck();
    }

    public virtual void StartEvents()
    {
        if (!IsPrimed)
        {
            Debug.LogWarning($"({this.gameObject.name}) Received StartEvents call, but was not primed.", this.gameObject);
            return;
        }

        if (IsRunning)
            StopEvents();

        IsPrimed = false;

        _runningEvents = StartCoroutine(ActivateEventsRoutine());
    }

    public virtual void StopEvents()
    {
        if (IsRunning)
            StopCoroutine(_runningEvents);
    }

    public virtual void Prime()
    {
        IsPrimed = true;
    }

    public virtual void PrimeUndo()
    {
        IsPrimed = false;
    }

    /// <summary>
    /// Destroys this gameobject. Useful for one-time GameEvents, once finished running.
    /// Alternative to disabling this gameobject.
    /// </summary>
    public virtual void Destroy()
    {
        Destroy(this.gameObject);
    }


    /// <summary> Event runner with timed delays between each event set. </summary>
    protected virtual IEnumerator ActivateEventsRoutine()
    {
        //StartActivateCooldownTimer();

        DebugLog("STARTING EVENTS", this.gameObject);

        int count = 0;
        foreach(EventsWithDelay puzEvent in _events)
        {
            if (puzEvent.Skip)
                continue;

            //Delay
            yield return new WaitForSeconds(puzEvent.Delay);

            //Run event
            puzEvent.Events?.Invoke();
            DebugLog($"INVOKING EVENT #{count} ({puzEvent.Name})", this.gameObject);

            //Count kept for DebugLog
            count++;
        }

        DebugLog($"FINISHED RUNNING EVENTS", this.gameObject);
        _runningEvents = null;

        if(afterDoneReprime)
        {
            IsPrimed = true;
        }

        if (afterDoneLoop)
        {
            IsPrimed = true;
            StartEvents();
        }

        yield return null;
    }


    protected virtual void OnTriggerEnter(Collider other)
    {
        if (activationMethod == Activate.OnTriggerEnter)
        {
            if (TriggerCheckObject(other))
            {
                DebugLog($"TriggerEnter: True: {_activationCondition.ToString()}");
                StartEvents();
            }
            else
            {
                DebugLog("TriggerEnter: False.");
            }
        }
    }


    protected virtual void OnTriggerExit(Collider other)
    {
        if (activationMethod == Activate.OnTriggerExit)
        {
            if (TriggerCheckObject(other))
            {
                DebugLog($"TriggerExit: True: {_activationCondition.ToString()}");
                StartEvents();
            }
            else
            {
                DebugLog("TriggerExit: False.");
            }
        }
    }


    protected virtual void OnCameraStartBlend()
    {
        if(activationMethod == Activate.OnCameraStartBlend)
        {
            StartEvents();
        }
    }


    protected virtual void OnCameraStopBlend()
    {
        if(activationMethod == Activate.OnCameraStopBlend)
        {
            StartEvents();
        }
    }

    protected virtual void OnPlayerInvisible()
    {
        if(activationMethod == Activate.OnPlayerInvisible)
        {
            StartEvents();
        }
    }


    protected virtual void OnPlayerVisible()
    {
        if (activationMethod == Activate.OnPlayerVisible)
        {
            StartEvents();
        }
    }


    protected virtual void DebugLog(string str, Object ctx = null)
    {
        //Allows for debug messages on/off toggle
        if(_eventDebugMessages)
            Debug.Log(str,ctx);
    }

    protected virtual void OnEnable()
    {
        GameCameraManager cameraManager = FindObjectOfType<GameCameraManager>();
        if (cameraManager)
        {
            cameraManager.OnStartBlend += OnCameraStartBlend;
            cameraManager.OnStopBlend += OnCameraStopBlend;
        }

        PlayerVisibleEvents playerVisibility = FindObjectOfType<PlayerVisibleEvents>();
        if(playerVisibility)
        {
            playerVisibility.OnPlayerVisible += OnPlayerVisible;
            playerVisibility.OnPlayerInvisible += OnPlayerInvisible;
        }

        if (room)
        {
            room.OnRoomEnter += OnRoomEnter;
            room.OnRoomExit += OnRoomExit;
        }
    }

    protected virtual void OnRoomEnter(GameObject someGameObject)
    {
        if (activationMethod == Activate.OnRoomEnter)
        {
            if (TriggerCheckObject(someGameObject))
                StartEvents();
        }
    }

    protected virtual void OnRoomExit(GameObject someGameObject)
    {
        if (activationMethod == Activate.OnRoomExit)
        {
            if (TriggerCheckObject(someGameObject))
                StartEvents();
        }
    }

    protected virtual void OnDisable()
    {
        GameCameraManager cameraManager = FindObjectOfType<GameCameraManager>();
        if (cameraManager)
        {
            cameraManager.OnStartBlend -= OnCameraStartBlend;
            cameraManager.OnStopBlend -= OnCameraStopBlend;
        }

        PlayerVisibleEvents playerVisibility = FindObjectOfType<PlayerVisibleEvents>();
        if(playerVisibility)
        {
            playerVisibility.OnPlayerVisible -= OnPlayerVisible;
            playerVisibility.OnPlayerInvisible -= OnPlayerInvisible;
        }

        if (room)
        {
            room.OnRoomEnter += OnRoomEnter;
            room.OnRoomExit += OnRoomExit;
        }


        if (IsRunning)
            StopCoroutine(_runningEvents);
    }

    protected void RoomErrorCheck()
    {
        if (activationMethod == Activate.OnRoomEnter || activationMethod == Activate.OnRoomExit)
        {
            string type = activationMethod.ToString();

            if (!room)
                Debug.LogError($"Room to check is null, but the Activation Method is set to {type}.", this.gameObject);
        }
    }

    /// <summary>
    /// Called from OnTriggerEnter/Exit to see if the collider meets the criteria specified from _triggerCondition.
    /// </summary>
    /// <returns></returns>
    protected bool TriggerCheckObject(Collider other)
    {
        return TriggerCheckObject(other.gameObject);
    }

    protected bool TriggerCheckObject(GameObject other)
    {
        if (!other)
            return false;

        switch (_activationCondition)
        {
            case ActivateCheck.NameEquals:
                return NameEquals(other.gameObject);
            case ActivateCheck.TagEquals:
                return TagEquals(other.gameObject);
            case ActivateCheck.IsPlayer:
                return IsPlayer(other.gameObject);
            case ActivateCheck.IsMonster:
                return IsMonster(other.gameObject);
            case ActivateCheck.IsMimic:
                return IsMimic(other.gameObject);
            case ActivateCheck.IsSoundhound:
                return IsSoundhound(other.gameObject);
            case ActivateCheck.IsSnail:
                return IsSnail(other.gameObject);
            default:
                return false;
        }
    }

    private bool IsPlayer(GameObject obj)
    {
        return (obj == GameManager.Instance.Player);
    }

    private bool IsMonster(GameObject obj)
    {
        return IsMimic(obj) || IsSoundhound(obj) || IsSnail(obj);
    }

    private bool IsMimic(GameObject obj)
    {
        return (obj.GetComponent<Monster_Mimic>() != null);
    }

    private bool IsSoundhound(GameObject obj)
    {
        return (obj.GetComponent<Soundhound>() != null);
    }

    private bool IsSnail(GameObject obj)
    {
        return (obj.GetComponent<SnailBehavior>() != null);
    }

    private bool NameEquals(GameObject obj)
    {
        return (obj.name == _activationConditionNameCheck);
    }

    private bool TagEquals(GameObject obj)
    {
        return (obj.tag == _activationConditionTagCheck);
    }
}

[System.Serializable]
public class EventsWithDelay
{
    /// <summary>
    /// Name in the Inspector.
    /// </summary>
    public string Name;

    /// <summary>
    /// The delay before running this set of Events.
    /// </summary>
    public float Delay;

    /// <summary>
    /// Allows an event to be skipped over. Set from the custom inspector created by GameEventEditor.
    /// </summary>
    [HideInInspector]
    public bool Skip;

    /// <summary>
    /// Some set of events to run. Ex: Turning on a light, spawning a monster at a position, etc.
    /// </summary>
    public UnityEvent Events;
}

enum Activate
{
    OnCall,
    OnTriggerEnter,         //Has an additional ActivateCheck
    OnTriggerExit,          //Has an additional ActivateCheck
    OnRoomEnter,            //Has an additional ActivateCheck
    OnRoomExit,             //Has an additional ActivateCheck
    OnCameraStartBlend,
    OnCameraStopBlend,
    OnPlayerInvisible,
    OnPlayerVisible
}

enum ActivateCheck
{
    IsPlayer,
    IsMonster,
    IsMimic,
    IsSoundhound,
    IsSnail,

    NameEquals,
    TagEquals
}