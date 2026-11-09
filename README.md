# Fluffy Climbers: Tower Jump

A 2D vertical-jumping arcade game for Android and iOS (Unity 6 LTS). Planning docs live in `E:\Source\Game Ideas\boing-up-sky-tower\docs\`.

> Closed-source commercial game (all rights reserved, see `LICENSE`). This repo also holds the public [privacy policy](POLICY.md) and [terms of use](TERMS.md), ready to publish with GitHub Pages (`index.md`, `_config.yml`). Hosting steps, store-console mapping and the release blockers the documents assume: [Docs/LEGAL-HOSTING.md](Docs/LEGAL-HOSTING.md). Any change to data handling (SDKs, permissions, analytics, server, notifications, ad networks, products) must update both documents and their "Last updated" date before the build ships.

## Status
Scaffold only: folder structure, content schemas, 30 regions and 450 levels of baseline data (from doc 08), and a content validation CLI. No Unity project files or gameplay code yet.

## Quick start
```
python Tools/content_cli/content.py validate
python -m unittest discover -s Tests/content -v
```
See `CLAUDE.md` for architecture rules and conventions.
