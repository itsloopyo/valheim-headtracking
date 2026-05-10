# Third-Party Notices

This project depends on the following third-party software.

---

## BepInEx

- **Version:** v5.4.23.5
- **License:** LGPL-2.1
- **Upstream:** https://github.com/BepInEx/BepInEx
- **Usage:** Mod loader framework. The vendored copy in `vendor/bepinex/` is bundled in the release ZIP and used as the install-time source by `install.cmd`.
- **Bundled:** yes. Bundled in release ZIP as the install-time source.

---

## OpenTrack

- **License:** ISC
- **Upstream:** https://github.com/opentrack/opentrack
- **Usage:** Head tracking data is received via the OpenTrack UDP protocol. No OpenTrack code is bundled or linked; only the wire protocol is implemented.
- **Bundled:** no.

---
