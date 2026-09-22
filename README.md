# TallyVel

> Keep your stokvel on track, together.

## Overview

TallyVel is a simple, no-fuss way to keep track of your stokvel — no more spreadsheets, WhatsApp screenshots, or arguments over who paid what.

Log contributions as they come in, see who's up to date and who's behind, and keep a clear record of your group's payout rotation — all in one place that every member can check anytime.

TallyVel doesn't move or hold your money. It's purely a tracking and record-keeping tool, built to give your stokvel transparency and peace of mind, without the risk or complexity of a payments platform.

## Key Features

- 📋 Track contributions per member, per cycle
- ✅ See at a glance who's paid, pending, or missed
- 🔄 Manage and view your payout schedule/rotation
- 🧾 Keep a clear, shared history everyone can trust
- 👥 Built for how real stokvels actually run

## Who It's For

Whether it's a small savings circle with friends or a larger community stokvel, TallyVel keeps everyone on the same page.

## Running the Project

The API lives in `TallyVel.Api` and targets .NET 10.

### From the terminal

```bash
cd TallyVel.Api
dotnet run
```

### From VS Code

Open the **Run and Debug** panel (⇧⌘D on Mac / Ctrl+Shift+D on Windows/Linux), select **TallyVel.Api** from the dropdown, and press F5. This builds the project and starts it with the debugger attached.

### Viewing the Scalar UI

Once the app is running in development, open:

```
http://localhost:5203/scalar/v1
```

This gives you an interactive reference for every API endpoint — no separate setup required.