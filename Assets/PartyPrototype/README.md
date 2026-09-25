# Android collision fix 0.4.1

Stop Play mode, import PartyPrototype-0.4.1.unitypackage, and rebuild/reinstall Android using the Party Prototype build menu. Include link.xml when importing. Verify HUD 0.4.1 on the phone.

The Android log explicitly reported: Can't add component because class SphereCollider doesn't exist! The runtime-created ball collider had been stripped from the player. The fix explicitly references and creates SphereCollider and BoxCollider, and adds linker preservation for the primitive rendering/physics components. Decorative markers have disabled box colliders rather than relying on delayed destruction.

Compilation and gameplay/network tests are checked independently; this APK still needs a phone collision check after rebuilding. On a neutral board, the ball should settle on the start lane instead of passing through it.

Original maps: all six Map 1 through Map 6 hierarchies were found in BLMain_Max_DF2.unity. Their referenced mesh assets resolve, and they contain box colliders and start/goal markers. They are technically recoverable for adaptation. This patch does not replace the simple prototype course with those maps yet; their transforms, goal logic, materials and collision behavior need porting and testing with the tilting board.

---

# Party Prototype 0.4.0

## Import and build

Stop Play mode and import PartyPrototype-0.4.0.unitypackage, replacing all included files. Keep the existing prototype scene. Wait for Unity compilation.

For Android, use Party Prototype > Build and Run Android (USB), or Build Android APK and manually install the resulting file. If Unity first switches to Android, wait for compilation and select the build command again. Both PC and phone must run this update.

The build uses CleanBuildCache. It also checks the finished APK for the actual HUD 0.4.0 string in compiled game code, rather than trusting its version number. An APK that fails this check must not be installed.

## Why the previous Android UI stayed old

Read-only inspection found the connected phone and APK both reported version 0.3.1, build code 5. But the APK global-metadata.dat still contained HUD 0.3.0 and The lobby, and lacked HUD 0.3.1 and PARTY LOBBY. The version stamp had changed while the compiled UI was stale. The precise Unity cache/platform-switch cause was not reproduced; clean builds, an explicit platform-switch/compile step, and output verification address this failure.

This task did not modify your project directly or install a new APK because project write access was not granted. Import this package and build from the project. The home screen should read HUD 0.4.0.

## Ball Tilt test

Connect at least two players. The host chooses Test Ball Tilt in the lobby. There is a three-second countdown followed by 60 seconds to reach the green finish.

- PC: WASD or arrow keys tilt the platform. Release to level it.
- Android: hold the phone comfortably, press Calibrate neutral tilt, and tilt gently. An accelerometer is required.
- The platform rotates as a kinematic rigidbody. The ball is a separate dynamic rigidbody: it is not parented to the platform and receives no steering forces.
- Follow the start lane to the middle bridge, then the finish lane. Falling respawns at the start; the timer continues.
- Reset ball restarts your local run without resetting the round timer.
- Everyone plays the same course locally. The host records incoming finishes and resolves after everyone finishes or time expires. First reported finish wins; everyone else gets one gulp. If nobody finishes, everyone gets one gulp.
- Next on the scoreboard ends this test, then the host can return to the lobby.

This is a standalone test round from the lobby, not yet part of the quiz/minigame random rotation. Finish reports trust the local physics client; host receipt timing includes network latency. There is no live spectating of other balls.

## Validation

Compiled runtime/editor code against installed Unity 6000.6.0f1 with zero warnings/errors. Passed 83 automated assertions covering quiz rules, actual local TCP networking, Ball Tilt countdown, finish validation, duplicate/late reports, winner scoring, and timeout outcomes.

The new Unity scene rendering, platform physics, accelerometer feel, APK build, and installation have not been exercised on a physical device in this turn. Test with PC + Android after import and verify HUD 0.4.0 on both.
