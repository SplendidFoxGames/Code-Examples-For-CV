using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System;

public class ReBindUITest : MonoBehaviour
{
    [SerializeField]
    private InputActionReference inputActionReference; // this is on the scriptable object

    [SerializeField]
    private bool excludeMouse = true;
    [Range(0, 10)]
    [SerializeField]
    private int selectedBinding;
    [SerializeField]
    private InputBinding.DisplayStringOptions displayStringOptions; // Format binding names to make it more readable
    [Header("Binding Info - DO NOT EDIT")]
    [SerializeField]
    private InputBinding inputBinding;
    private int bindingIndex;

    private string actionName;  // Jump, Kick, Punch, etc. Each action can have multiple inputs. K, ButtonSouth, LeftMouseButton, etc.

    [Header("UI Fields")]
    [SerializeField]
    private TextMeshProUGUI actionText;
    [SerializeField]
    private Button rebindButton;
    [SerializeField]
    private TextMeshProUGUI rebindText;
    public TextMeshProUGUI ReBindText
    {
        get => rebindText;
    }

    public Color RebindTextcolour
    {
        get => rebindText.color;
        set => rebindText.color = value;
    }

    [SerializeField]
    private Button resetButton;
    [Tooltip("Optional UI that will be shown while a rebind is in progress.")]
    [SerializeField] private GameObject m_RebindOverlay;
    [SerializeField] private GameObject m_RebindModifierOverlay;

    public event Action<ReBindUITest, string, string, string> updateGamePadIcon;

    //test
    public static PlayerControls inputActions;

    private void OnEnable()
    {
        rebindButton.onClick.AddListener(() => DoRebind());
        resetButton.onClick.AddListener(() => ResetBinding());
        var thisThing = gameObject;

        if (inputActionReference != null)
        {
            GetBindingInfo();
            UpdateUI();
            InputManager.LoadBindingOverride(actionName);
        }

        InputManager.rebindComplete += UpdateUI;
        InputManager.rebindComplete += CloseRebindOverlay;
        InputManager.rebindCancelled += UpdateUI;
        InputManager.rebindCancelled += CloseRebindOverlay;
        InputManager.rebindStarted += RebindStarted;
        InputManager.rebindModifierStarted += RebindModifierStarted;


        if (inputActions == null)
            inputActions = new PlayerControls();
        InputAction action = inputActions.asset.FindAction(actionName);
 }

    private void OnDisable()
    {
        InputManager.rebindComplete -= UpdateUI;
        InputManager.rebindComplete -= CloseRebindOverlay;
        InputManager.rebindCancelled -= UpdateUI;
        InputManager.rebindCancelled -= CloseRebindOverlay;
        InputManager.rebindStarted -= RebindStarted;
        InputManager.rebindModifierStarted -= RebindModifierStarted;
    }

    // Called when anything changes in the inspector
    // BUT THIS ALSO NEEDS TO BE CALLED WHEN THE GAME IS STARTED STANDALONE
    private void OnValidate()
    {
        if (inputActionReference == null)
            return;
        GetBindingInfo();
        UpdateUI();
    }

    private void GetBindingInfo()
    {
        if (inputActionReference.action != null)
        {
            // If composite, get the name, not the action name
            actionName = inputActionReference.action.name;  // Jump, etc
        }


        if (inputActionReference.action.bindings.Count > selectedBinding)
        {
            inputBinding = inputActionReference.action.bindings[selectedBinding];
            bindingIndex = selectedBinding;
        }
    }

    private void UpdateUI()
    {
        if (actionText != null)
        {
            if (inputBinding.isPartOfComposite)
                actionText.text = inputActionReference.action.bindings[selectedBinding].name;
            else
                actionText.text = actionName;
        }


        if (rebindText != null)
        {
            if (Application.isPlaying)
            {
                rebindText.text = InputManager.GetBindingName(actionName, bindingIndex);    // When starting a game in editor, this gets hit and will initialise the inputActions
            }
            else
                rebindText.text = inputActionReference.action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions); // When updating in editor
        }

        UpdateIcons();
    }

    private void DoRebind()
    {
        InputManager.StartRebind(actionName, bindingIndex, rebindText, excludeMouse); // actionName = 'Jump' for example
    }

    public void ResetBinding(bool skipCheckingForConflicts = false)
    {
        InputManager.ResetBinding(actionName, bindingIndex, skipCheckingForConflicts);
        UpdateUI();
    }

    private void RebindStarted()
    {
        m_RebindOverlay?.SetActive(true);
    }

    private void RebindModifierStarted()
    {
        m_RebindOverlay?.SetActive(false);
        m_RebindModifierOverlay?.SetActive(true);
    }

    private void CloseRebindOverlay()
    {
        m_RebindOverlay?.SetActive(false);
        m_RebindModifierOverlay?.SetActive(false);
    }

    public void UpdateIcons()
    {
        var displayString = string.Empty;
        var deviceLayoutName = default(string);
        var controlPath = default(string);

        if (actionName == null)
            GetBindingInfo();   // For some reason the binding actionName is null for most gamepad actions upon first load. On second load it works. So added this to just get the info if any of them are null

        displayString = InputManager.GetBindingName(actionName, bindingIndex, out deviceLayoutName, out controlPath);
        // Now tell GamePadIcons class about it
        updateGamePadIcon?.Invoke(this, displayString, deviceLayoutName, controlPath);
    }
}
