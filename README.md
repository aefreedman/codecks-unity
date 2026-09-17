# Codecks Bug & Feedback Reporter for Unity

Collect bugs and feedback right from your Unity game and keep players in the loop what happens with their feedback.

This is an independently maintained fork of the original Codecks Unity plugin. It is not affiliated with or endorsed by Codecks GmbH.

## Documentation

Package documentation is available in [Documentation~](./Documentation~/index.md). The original Codecks user-report documentation remains available at the [Codecks Manual Page](https://manual.codecks.io/user-reports/).


## Set up

Install this repository as a UPM package, then import the **Feedback Reporter (uGUI)** sample from Package Manager. The imported sample contains `CodecksSampleScene` with the default layout and setup. Its required Unity UI and TextMesh Pro dependencies are declared by the package.

## Getting started

The sample scene already provides all the UI elements and component setup for you to get started right away. You can test the initial setup of the report tool right from the sample scene by entering your created report token (the one that you created in your Codecks User Report settings screen) into the `Default Token` property of the `CodecksCardCreator` component on the `CardCreator` scene object (found under the `Canvas` object). Once you've done that you can hit _Play_. Click the "Give Feedback!" button, fill out the form and press "Send report". If everything works, you should see a card pop up in your Codecks just moments later. In case of issues, an error message should be printed to your screen and console.

## Adapting it to your own needs

After testing the initial setup, we recommend copying or integrating the sample scene into your own UI game scene where you can configure it to show up when pressing a hotkey or by selecting a menu entry according to your own needs. You may also modify the layout to fit your game thematically or use the Codecks default layout as provided. In any case please make sure to not hide the `Powered by Codecks` sprite and display it next to the report form.

Here's an explanation what the two provided MonoBehavior classes do:

- **CodecksCardCreator** handles the basic API communication with Codecks for the purpose of creating cards inside your Codecks project. This class does not handle any UI related tasks and contains only the basic functionality.
- **CodecksCardCreatorForm** is a helper class that manages the UI and forwards the input to the `CodecksCardCreator` class. You may write your own UI handling in case you're not using the default Unity Canvas system or in case you have special requirements for your UI. The class provides a method `GetMetaText` which you can edit to add your own game related meta data. By default the component also creates a screenshot and attaches it to the request sent to the `CodecksCardCreator` class. You may choose to add additional files to the request (e.g. attaching a savegame or world state dump).

## License

The code is licensed under the MIT license. See [`LICENSE.md`](./LICENSE.md).

## Contribute

### Docs

The sources for the docs can be found in [`Documentation~/index.md`](./Documentation~/index.md).

To create a PDF you need node v14+ installed on your machine. Run this command from [`Documentation~`](./Documentation~/):

```sh
cat ./index.md | npx md-to-pdf > ./Codecks\ Unity\ Plugin\ Manual.pdf
```
