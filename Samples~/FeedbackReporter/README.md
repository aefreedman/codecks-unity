# Feedback Reporter (uGUI)

Import this sample from **Window > Package Manager > Codecks Bug & Feedback Reporter > Samples**, then open `CodecksSampleScene` from the imported `FeedbackReporter` folder. The sample contains no report token or other credentials.

## Run the sample

1. With imported sample scenes closed, run **Tools > Codecks > Set Up Imported Samples**. This creates/selects `Assets/Resources/Codecks/CodecksSettings.asset` and wires missing references in standard imported sample scenes. Configure its report token/endpoint. Repeated setup preserves existing assets and references; open scenes are skipped.
2. If the text in the scene is missing, use **Window > TextMeshPro > Import TMP Essential Resources**. The sample uses the standard TMP Liberation Sans font asset supplied by those resources.
3. Enter Play mode, select **Give Feedback!**, enter a description of at least ten characters, then select **Send Report**.

The backend requires an explicit **Settings** asset reference; automatic Resources and legacy token-file/component fallback are removed. Hidden obsolete token/endpoint fields are retained only for migration compatibility and are ignored. For customized scenes outside standard `Assets/Samples/` paths, explicitly assign your settings asset; bootstrap does not touch user scenes. Missing references and empty/invalid settings fail once before dispatch. Tokens are never copied automatically and remain extractable from builds; do not commit configured assets. Legacy `CodecksTokenCreator.CreateAndSetNewToken` writes a token file that is no longer used by the reporter: trusted tooling must configure the referenced asset instead. Keep access keys out of builds and source control.

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
