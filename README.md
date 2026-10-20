# Boing Up: Sky Tower

A 2D vertical-jumping arcade game for Android and iOS (Unity 6 LTS). Planning docs live in `E:\Source\Game Ideas\boing-tower-docs\docs\`.

## Status
Scaffold only: folder structure, content schemas, 30 regions and 450 levels of baseline data (from doc 08), and a content validation CLI. No Unity project files or gameplay code yet.

## Quick start
```
python Tools/content_cli/content.py validate
python -m unittest discover -s Tests/content -v
```
See `CLAUDE.md` for architecture rules and conventions.
