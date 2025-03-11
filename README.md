# Drawing Names Composer

A Tekla Structures Extension that allows you to compose custom filenames when printing drawings.

## Problem Solved
When printing multiple drawings in Tekla Structures with identical filenames, the system generates errors. The standard solution requires modifying the `XS_DRAWING_PLOT_FILE_NAME_*` Advanced Properties, which can cause conflicts in shared models.

Drawing Names Composer eliminates this problem by enabling custom filename composition using multiple drawing properties of your choice.

## Screenshots
![User interface of Drawing Names Composer](https://github.com/user-attachments/assets/7f598dce-6748-49aa-a303-4c7f5e1473c0)

![Property selection interface](https://github.com/user-attachments/assets/c489dc76-eb72-4ec2-9deb-0393ba8218f4)

## Features
- Scans model folders for `PrintingSettings.xml` files to extract available properties
- Drag-and-drop interface for composing custom filenames
- Properties are surrounded by '%' in the composition field
- Full configuration management (Save, Load, Save As) compatible with Tekla's workflow

## Configuration
Saved configurations are stored in `%LocalAppData%\BitLuz\drawing_names_composer`

## Compatibility
- Currently compatible with Tekla Structures 2024
- Requires recompilation for other versions

## Status
This is a functional but evolving project. Contributions and feedback for improvements are welcome.
