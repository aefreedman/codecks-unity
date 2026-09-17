# Feedback Reporter (uGUI)

Import this sample from **Window > Package Manager > Codecks Bug & Feedback Reporter > Samples**, then open `CodecksSampleScene` from the imported `FeedbackReporter` folder. The sample contains no report token or other credentials.

## Run the sample

1. Select `Canvas/CardCreator` and set **Default Token** to a Codecks report token.
2. If the text in the scene is missing, use **Window > TextMeshPro > Import TMP Essential Resources**. The sample uses the standard TMP Liberation Sans font asset supplied by those resources.
3. Enter Play mode, select **Give Feedback!**, enter a description of at least ten characters, then select **Send Report**.

`CodecksCardCreator` first uses `Resources/Codecks/codecksToken.txt` when present; otherwise it uses **Default Token**. `CodecksTokenCreator.CreateAndSetNewToken` is intended for a trusted editor or build script and writes that resource file. Keep the access key out of source control and out of player builds; report tokens are the values intended to ship in a build.

## Input

The scene includes an `EventSystem` with `StandaloneInputModule`, which supports projects using the legacy Input Manager or **Active Input Handling: Both**. It does not add an Input System package dependency.

For a project using only the new Input System, install `com.unity.inputsystem`, replace `StandaloneInputModule` on the sample's `EventSystem` with `InputSystemUIInputModule`, and assign or create that module's UI action references. Do not leave both input modules enabled on the same EventSystem.

## Customize metadata outside PackageCache

`CodecksCardCreatorForm.GetMetaText()` is a protected virtual method. Create a script in your own project's `Assets` folder, inherit from `CodecksCardCreatorForm`, override the method, and replace the `CodecksCardCreatorForm` component on the imported form with your subclass. Keep the existing serialized UI references assigned.

```csharp
using Codecks.Runtime;

public sealed class ProjectFeedbackForm : CodecksCardCreatorForm
{
    protected override string GetMetaText()
    {
        return base.GetMetaText() + "Build channel: internal\n";
    }
}
```

This keeps custom behavior durable across package updates. You can also use `CodecksCardCreator` directly when supplying a completely custom UI.

Keep the Powered by Codecks mark visible next to the form when adapting this sample.
