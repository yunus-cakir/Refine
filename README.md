# Refine

**Refine** is an offline-first workout tracking and personal development application built with **.NET MAUI Blazor Hybrid**. The application combines native mobile capabilities with modern web-based UI technologies to provide an intuitive training experience focused on workout planning, performance tracking, recovery management, and body composition analytics.

## Features

### Workout Tracking

* Create and manage workout programs
* Multi-week training cycles and progression
* Active workout sessions with real-time logging
* Support for sets, reps, weight, and RIR (Reps in Reserve)

### Recovery Monitoring

* Central Nervous System (CNS) fatigue tracking
* Muscle-specific fatigue analysis
* Recovery recommendations based on training load
* Visual recovery dashboards

### Exercise Database

* Extensive pre-seeded exercise library
* Equipment classification system
* Muscle impact mapping
* Exercise taxonomy designed for recovery calculations

### Progress Analytics

* Strength progression tracking
* Estimated 1RM calculations
* Progressive overload analysis
* Historical workout insights

### Body Composition Tracking

* BMI calculation
* U.S. Navy body fat estimation
* Biometric logging and trend analysis
* Historical body composition tracking

### Offline-First Experience

* Fully functional without internet connectivity
* Embedded SQLite database
* Fast local data access
* Reliable workout logging anywhere

---

## Architecture

Refine is built using a **.NET MAUI Blazor Hybrid** architecture.

### Native Layer (.NET MAUI)

Responsible for:

* Application lifecycle management
* Native navigation shell
* Hardware back button handling
* Safe area management
* Native UI chrome (top bar and bottom navigation)

### UI Layer (Blazor)

Responsible for:

* Interactive user interface
* State management
* Data binding
* Business logic execution
* Analytics visualization

### Architecture Overview

```text
UI Layer (Blazor Components)
            ↓
Service Layer
            ↓
Business Logic & Analytics
            ↓
SQLite Database
```

---

## Design Patterns

### Bridge Pattern

Used to synchronize native MAUI navigation elements with Blazor components.

Examples:

* NavigationBridge
* TopBarBridge

### Service Layer Pattern

All database access and calculations are abstracted behind services.

Examples:

* LocalDbService
* RecoveryService
* AnalyticsService

### State Management Pattern

A centralized state store prevents redundant database queries and manages asynchronous dashboard loading.

Example:

* HomeStateService

### Strategy Pattern

Analytics calculations dynamically switch between different progression metrics without large conditional blocks.

---

## Database Design

Refine uses:

* SQLite
* sqlite-net-pcl
* SQLiteNetExtensionsAsync

### Key Characteristics

* Offline-first architecture
* Relational entity structure
* Transaction support
* Automatic cascading operations
* Concurrency-safe initialization

### Exercise Classification

Exercises are categorized by:

* Equipment Type
* Laterality Type
* Muscle Group Mapping
* Activation Type

Each exercise contains muscle impact multipliers that allow the system to estimate localized fatigue and recovery needs.

---

## Recovery System

One of Refine's core features is its recovery modeling system.

### CNS Fatigue

Each exercise has a predefined fatigue score.

Examples:

| Exercise      | CNS Score |
| ------------- | --------- |
| Deadlift      | 9.5       |
| Dumbbell Curl | 3.0       |

The final fatigue value is adjusted using training intensity derived from the user's RIR.

### Muscle Recovery

The application calculates muscle fatigue by:

1. Analyzing recent training volume
2. Applying exercise-specific impact multipliers
3. Comparing volume against MRV-based thresholds
4. Generating fatigue percentages for individual muscle groups

---

## Active Session Experience

The workout screen is designed specifically for gym environments.

### Reels-Style Navigation

The active workout experience uses:

* Vertical exercise pages
* Horizontal set navigation
* CSS Scroll Snapping
* Automatic progression between sets

This approach minimizes distractions and reduces cognitive load during training.

### Session Protection

To prevent accidental data loss:

* Navigation locks intercept exits
* Confirmation dialogs require user action
* Progress can be saved before leaving a session

---

## Technology Stack

### Frontend

* .NET MAUI
* Blazor Hybrid
* Razor Components
* Scoped CSS

### Backend Logic

* C#
* Dependency Injection
* Service-Oriented Architecture

### Database

* SQLite
* sqlite-net-pcl
* SQLiteNetExtensionsAsync

### Native Integration

* JavaScript Interop
* Local Storage
* Hardware Navigation Handling

---

## Getting Started

### Prerequisites

* .NET 9 SDK (or project target version)
* Visual Studio 2022+
* MAUI Workload Installed

### Clone the Repository

```bash
git clone https://github.com/yourusername/refine.git
cd refine
```

### Restore Dependencies

```bash
dotnet restore
```

### Run the Application

```bash
dotnet build
dotnet run
```

Or launch directly from Visual Studio using an Android emulator or a physical device.

---

## Future Roadmap

### Planned Improvements

* Cloud synchronization
* Cross-device support
* Advanced periodization systems
* Automated deload recommendations
* Wearable device integration
* Heart-rate-based recovery estimation
* Enhanced analytics dashboards

---

## Project Goals

Refine aims to go beyond traditional workout logging applications by combining:

* Training management
* Recovery science
* Performance analytics
* Body composition tracking

into a single, offline-first mobile experience designed for athletes and fitness enthusiasts.

---

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.
