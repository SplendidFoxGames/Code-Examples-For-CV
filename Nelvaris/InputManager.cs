using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using TMPro;
using System.Linq;

public class InputManager : MonoBehaviour
{
    public static PlayerControls inputActions;

    public static event Action rebindComplete;
    public static event Action rebindCancelled;
    public static event Action rebindStarted;
    public static event Action rebindModifierStarted;

    public static string originalActionToRebindPath = ""; // Saves the path of the action being rebinded before it happens, so that it can be swapped with any potential conflcits after binding occurs

    private static ReBindUITest[] theUIKeyBindings;
    private static ReBindUITest[] keyboardUIKeyBindings;
    private static ReBindUITest[] gamePadUIKeyBindings;
    private static ReBindUITest[] mouseUIKeyBindings;
    private static Color defaultButtonColour = new Color(0.2980392f, 0.05882353f, 0.4901961f);
    //private static Color defaultButtonColour = Color.white;
    private static Color conflictButtonColour = Color.red;

    private void Awake()
    {
        if (inputActions == null)        // Will most likely never be null
            inputActions = new PlayerControls();
    }
    public static void InitialisePlayerControls()
    {
        inputActions = new PlayerControls();    
    }
    public static void StartRebind(string actionName, int bindingIndex, TextMeshProUGUI statusText, bool excludeMouse)
    {
        InputAction action = inputActions.asset.FindAction(actionName);
        if (action == null || action.bindings.Count <= bindingIndex)
        {
            Debug.Log("Couuldnt find action or binding");
            return;
        }

        if (action.bindings[bindingIndex].isComposite)
        {
            var firstPartIndex = bindingIndex + 1;  // Composit bindings are, for example, WASD or 2D Vector. Don't rebind the WASD parent, but the sub parts, such as W, A, S, and D.
            var name = action.bindings[firstPartIndex].name;
            if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                DoRebind(action, firstPartIndex, statusText, true, excludeMouse);
        }
        else
            DoRebind(action, bindingIndex, statusText, false, excludeMouse);
    }

    private static void DoRebind(InputAction actionToRebind, int bindingIndex, TextMeshProUGUI statusText, bool allCompositeParts, bool excludeMouse)
    {
        if (actionToRebind == null || bindingIndex < 0)
            return;

        //statusText.text = $"Press a {actionToRebind.expectedControlType}";

        ////Debug.Log("Path: " + actionToRebind.bindings[bindingIndex].path);
        ////Debug.Log("OverridePath: " + actionToRebind.bindings[bindingIndex].overridePath);
        ////Debug.Log("EffectivePath: " + actionToRebind.bindings[bindingIndex].effectivePath);
        ////Debug.Log("\n\n");

        if (actionToRebind.bindings[bindingIndex].isPartOfComposite)
        {
            statusText.text = $"Binding '{actionToRebind.bindings[bindingIndex].name}'. ";

        }
        else
            statusText.text = "Press a " + actionToRebind.expectedControlType;
        actionToRebind.Disable();
        inputActions.UI.Disable();

        var rebind = actionToRebind.PerformInteractiveRebinding(bindingIndex);

        rebind.OnComplete(operation =>
        {
            ////Debug.Log("Path: " + operation.action.bindings[bindingIndex].path);
            ////Debug.Log("OverridePath: " + operation.action.bindings[bindingIndex].overridePath);
            ////Debug.Log("EffectivePath: " + operation.action.bindings[bindingIndex].effectivePath);

            //actionToRebind.Enable();

            inputActions.UI.Enable();
            operation.Dispose();
            SaveBindingOverride(actionToRebind);
            CheckForControlConflicts();
            rebindComplete?.Invoke();

            if (allCompositeParts)
            {
                var nextBindingIndex = bindingIndex + 1;
                if (nextBindingIndex < actionToRebind.bindings.Count && actionToRebind.bindings[nextBindingIndex].isPartOfComposite)
                {
                    DoRebind(actionToRebind, nextBindingIndex, statusText, allCompositeParts, excludeMouse);
                    rebindModifierStarted?.Invoke();
                }
            }
        });

        rebind.OnCancel(operation =>
        {
            //actionToRebind.Enable();
            inputActions.UI.Enable();
            operation.Dispose();
            rebindCancelled?.Invoke();
        });

        rebind.OnPotentialMatch(operation =>
        {
            if (operation.selectedControl.path is "/Keyboard/escape")
                operation.Cancel();
            return;
        });

        rebind.WithCancelingThrough("an enormous string of absolute gibberish which overrides the default which is escape and causes the above bug");
        rebind.WithCancelingThrough("<Gamepad>/start");
        //rebind.WithExpectedControlType("axis"); //add this to bind to scroll wheel
        //rebind.WithControlsExcluding("<Pointer>/delta");

        if (excludeMouse)
            rebind.WithControlsExcluding("Mouse");

        rebindStarted?.Invoke();    // Can be used with UI. For example when UI subscribes to this event, we can have a pop up appear with the name of the action being rebound
        rebind.Start(); // Actually starts the rebinding process
        originalActionToRebindPath = actionToRebind.bindings[bindingIndex].overridePath;
    }

    public static string GetBindingName(string actionName, int bindingIndex)
    {
        if (inputActions == null)
            inputActions = new PlayerControls();

        InputAction action = inputActions.asset.FindAction(actionName);
        return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }

    public static string GetBindingName(string actionName, int bindingIndex, out string deviceLayoutName, out string controlPath)
    {
        if (inputActions == null)
            inputActions = new PlayerControls();

        InputAction action = inputActions.asset.FindAction(actionName);
        return action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath);
    }

    public static void CheckForControlConflicts()
    {
        var allBindings = inputActions.Player.Get().bindings;

        List<InputBinding> bindingsList = new List<InputBinding>(allBindings); // "Jump:<Keyboard>/space[Keyboard]", "Kick:<Gamepad>/buttonEast[Gamepad]", etc
        theUIKeyBindings = FindObjectsOfType<ReBindUITest>();

        // as it's difficult to tell when a particular conflict has been cleared. We clear all conflicts, then recheck them
        // Clear all conflicted colours 
        for (int i = 0; i < theUIKeyBindings.Length; i++)
        {
            var colours = theUIKeyBindings[i].GetComponentInChildren<Button>().colors;
            colours.normalColor = defaultButtonColour;
            theUIKeyBindings[i].GetComponentInChildren<Button>().colors = colours;            
        }
        FindObjectOfType<UIConflictText>().GetComponent<TextMeshProUGUI>().enabled = false;

        // Check all bindings for conflicts
        for (int i = 0; i < bindingsList.Count; i++)
        {
            // Dont compare if has no effective path. If has not been rebound.
            if (bindingsList[i].effectivePath == null || bindingsList[i].isComposite) 
                continue;

            for (int j = 0; j < bindingsList.Count; j++)
            {             
                // Dont compare against self
                // Dont compare if has no effective path. If has not been rebound.
                if (i == j || bindingsList[j].effectivePath == null || bindingsList[j].isComposite) 
                    continue;
                // Only compare parts of composites with parts of compoaites
                if(bindingsList[i].isPartOfComposite && !bindingsList[j].isPartOfComposite)
                    continue;
                if (!bindingsList[i].isPartOfComposite && bindingsList[j].isPartOfComposite)
                    continue;

                // Compare Modifiers with Modifiers. alt+J, etc. the alt is name = modifier. the j is flag = partOfComposite
                if (bindingsList[i].name == "modifier" && bindingsList[j].name == "modifier")
                {
                    // Example: "<Keyboard>/alt + <Keyboard>/j"
                    string action1Path = bindingsList[i].effectivePath + " + " + bindingsList[i + 1].effectivePath;
                    string action2Path = bindingsList[j].effectivePath + " + " + bindingsList[j + 1].effectivePath;
                    // Then find another modifier with the same binding
                    // then call conflict
                    if (action1Path == action2Path)
                    {
                        Debug.Log("Conflict with one modifier keys " + bindingsList[i].action + " and " + bindingsList[j].action);
                        if (bindingsList[i].effectivePath.ToLower().Contains("keyboard"))
                        {
                            if(keyboardUIKeyBindings == null)
                                keyboardUIKeyBindings = FindObjectOfType<UIKeyboardControlsParent>().GetComponentsInChildren<ReBindUITest>();
                            EnableConflictColourAndMessage(bindingsList, i, j, keyboardUIKeyBindings);
                        }                            
                        else if (bindingsList[i].effectivePath.ToLower().Contains("gamepad"))
                        {
                            if (gamePadUIKeyBindings == null)
                                gamePadUIKeyBindings = FindObjectOfType<UIGamePadControlsParent>().GetComponentsInChildren<ReBindUITest>();
                            EnableConflictColourAndMessage(bindingsList, i, j, gamePadUIKeyBindings);
                        }                            
                    }
                }

                

                if (bindingsList[i].effectivePath == bindingsList[j].effectivePath)
                {
                    // Don't compare binding with modifier against a binding with no modifier
                    if (IsBindingPartOfOneModifier(bindingsList[i]) && !IsBindingPartOfOneModifier(bindingsList[j]))
                        continue;
                    if (IsBindingPartOfOneModifier(bindingsList[j]) && !IsBindingPartOfOneModifier(bindingsList[i]))
                        continue;

                    if (bindingsList[i].effectivePath.ToLower().Contains("keyboard") ||
                        bindingsList[i].effectivePath.ToLower().Contains("mouse"))
                    {
                        if (keyboardUIKeyBindings == null)
                            keyboardUIKeyBindings = FindObjectOfType<UIKeyboardControlsParent>()?.GetComponentsInChildren<ReBindUITest>();
                        // This is because this code is called when the pause menu is open, because the InputManager will be set active,
                        // even before the controls can be seen
                        // and so it will throw an error. but we dont need it there
                        // we only need it to be used when actually on the controls screen
                        if (keyboardUIKeyBindings == null)
                            continue;
                        EnableConflictColourAndMessage(bindingsList, i, j, keyboardUIKeyBindings);
                    }
                    else if (bindingsList[i].effectivePath.ToLower().Contains("gamepad"))
                    {
                        if (gamePadUIKeyBindings == null)
                            gamePadUIKeyBindings = FindObjectOfType<UIGamePadControlsParent>()?.GetComponentsInChildren<ReBindUITest>();
                        if (gamePadUIKeyBindings == null)
                            continue;
                        EnableConflictColourAndMessage(bindingsList, i, j, gamePadUIKeyBindings);
                    }
                }
            }
        }
    }

    private static bool IsBindingPartOfOneModifier(InputBinding binding)
    {
        if (binding.name == "modifier" && binding.isPartOfComposite) // This is part of One Modifier. Only compare one modifiers against one modifiers
            return true;
        if (binding.name == "binding" && binding.isPartOfComposite) // This is part of One Modifier. Only compare one modifiers against one modifiers
            return true;
        else
            return false;
    }

    public static void EnableConflictColourAndMessage(List<InputBinding> bindingsList, int i, int j, ReBindUITest[] uiBindings)
    {
        if (bindingsList[i].isPartOfComposite)
        {
            ReBindUITest result1 = null;
            if (bindingsList[i].name == "modifier")
            {
                result1 = (from b in uiBindings where b.name == bindingsList[i].action select b).SingleOrDefault();
            }
            else
            {
                result1 = (from b in uiBindings where b.name == bindingsList[i].name select b).SingleOrDefault();
            }
            if (result1 != null)
            {
                var colours = result1.GetComponentInChildren<Button>().colors;
                colours.normalColor = conflictButtonColour;
                result1.GetComponentInChildren<Button>().colors = colours;
            }
                
        }
        else // else get action
        {
            ReBindUITest result1 = (from b in uiBindings where b.name == bindingsList[i].action select b).SingleOrDefault();
            var colours = result1.GetComponentInChildren<Button>().colors;
            colours.normalColor = conflictButtonColour;
            result1.GetComponentInChildren<Button>().colors = colours;
        }



        // NOTE If composite and name is modifier, then it it will be the alt part of the binding alt+j.TThe j part will be labelled as binding
        if (bindingsList[j].isPartOfComposite)
        {
            ReBindUITest result2 = null;

            if (bindingsList[j].name == "modifier")
            {
                result2 = (from b in uiBindings where b.name == bindingsList[j].action select b).SingleOrDefault();
            }
            else
            {
                result2 = (from b in uiBindings where b.name == bindingsList[j].name select b).SingleOrDefault();
            }
            if (result2 != null)
            {
                //result2.RebindTextcolour = conflictButtonColour;
                var colours = result2.GetComponentInChildren<Button>().colors;
                colours.normalColor = conflictButtonColour;
                result2.GetComponentInChildren<Button>().colors = colours;
            }
                
        }
        else
        {
            ReBindUITest result2 = (from b in uiBindings where b.name == bindingsList[j].action select b).SingleOrDefault();
            //result2.RebindTextcolour = conflictButtonColour;
            var colours = result2.GetComponentInChildren<Button>().colors;
            colours.normalColor = conflictButtonColour;
            result2.GetComponentInChildren<Button>().colors = colours;
        }

        FindObjectOfType<UIConflictText>().GetComponent<TextMeshProUGUI>().enabled = true;
    }
    public static void SaveBindingOverride(InputAction action)
    {
        string testKey = "";
        string testvalue = "";
        for (int i = 0; i < action.bindings.Count; i++)
        {
            testKey = action.actionMap + action.name + i;
            testvalue = action.bindings[i].overridePath;
            PlayerPrefs.SetString(action.actionMap + action.name + i, action.bindings[i].overridePath);
        }
    }

    public static void LoadBindingOverride(string actionName)
    {
        if (inputActions == null)
            inputActions = new PlayerControls();

        InputAction action = inputActions.asset.FindAction(actionName);

        try
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string binding = action.actionMap + action.name + i;
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(action.actionMap + action.name + i)))
                    action.ApplyBindingOverride(i, PlayerPrefs.GetString(action.actionMap + action.name + i));
            }
        }
        catch (Exception)
        {

            throw;
        }

    }

    public static void ResetBinding(string actionName, int bindingIndex, bool skipCheckingForConflicts = false)
    {
        InputAction action = inputActions.asset.FindAction(actionName);

        if (action == null || action.bindings.Count <= bindingIndex)
        {
            Debug.Log("Could not find aciton or binding");
            return;
        }

        if (action.bindings[bindingIndex].isComposite)
        {
            for (int i = bindingIndex; i < action.bindings.Count && action.bindings[i].isComposite; i++)
                action.RemoveBindingOverride(i);

            // For actions with one button modifiers, for example alt+j
            for (int i = bindingIndex; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].name == "modifier")
                {
                    action.RemoveBindingOverride(i);    // Remove the modifier "alt"
                    action.RemoveBindingOverride(i + 1);  // Remove the binding "j"
                }
            }
        }
        else
            action.RemoveBindingOverride(bindingIndex);

        SaveBindingOverride(action);
        if (skipCheckingForConflicts == false)
            CheckForControlConflicts();
    }

    public static void ResetAllBindings()
    {
        foreach (var action in theUIKeyBindings)
        {
            action.ResetBinding(true);
            InputManager.CheckForControlConflicts();
        }        
    }

    /// <summary>
    /// Called when the pause menu opens the controls page
    /// </summary>
    public static void FindKeyboardRebindButtons()
    {
        keyboardUIKeyBindings = FindObjectOfType<UIKeyboardControlsParent>()?.GetComponentsInChildren<ReBindUITest>();
        CheckForControlConflicts();
    }

    public static void FindGamepadRebindButtons()
    {
        gamePadUIKeyBindings = FindObjectOfType<UIGamePadControlsParent>()?.GetComponentsInChildren<ReBindUITest>();
        CheckForControlConflicts();
    }

}

