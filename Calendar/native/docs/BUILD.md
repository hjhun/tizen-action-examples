# Calendar Native Build Instructions

This document provides instructions on how to build and package the native C++17 EFL port of the Calendar application.

## Prerequisites
- Tizen Studio CLI (`tizen`)
- Rootstrap: `tizen-10.0-emulator64.core` (or equivalent target)
- Action compiler (`actionc`) installed and in `$PATH`

## Host Tests
To build and run host tests for the domain and usecase logic, run:
```bash
make
./build/calendar-host-tests
```

## Device Build
To build the native application using the Tizen CLI:

```bash
tizen build-native -a x86_64 -c gcc -C Debug -r tizen-10.0-emulator64.core
```

## Packaging

Choose the signing mode explicitly for the target.

For the Public Tizen Common Emulator, intentionally omit `-s`. Tizen Studio
then applies its built-in emulator-only signature; this is not a custom
certificate profile and must not be used for distribution:

```bash
tizen package -t tpk -o <output-dir> -- Debug
```

For a custom signing target, select the intended profile explicitly and first
verify that its PKCS#12 material is valid:

```bash
tizen package -t tpk -s tizen-action-dev -o <output-dir> -- Debug
```

If the custom profile reports a keystore or certificate error, treat that mode
as blocked. Do not silently claim the emulator-only package used the custom
profile.

## Installation and Execution
Install to emulator:
```bash
sdb -s emulator-26101 install Debug/org.tizen.actionexamples.calendar-0.1.0-x86_64.tpk
```

Launch on emulator:
```bash
sdb -s emulator-26101 shell app_launcher -s org.tizen.actionexamples.calendar
```
