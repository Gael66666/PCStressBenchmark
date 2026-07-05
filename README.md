\# PC Stress Benchmark



Un outil de benchmark et de test de charge matériel moderne développé en \*\*.NET 8.0\*\* et \*\*WPF\*\*\[cite: 7]. Il permet de tester la stabilité et les performances du processeur (CPU), de la mémoire vive (RAM) et de la carte graphique (GPU) tout en surveillant la température\[cite: 1, 6].



\---



\## 🚀 Fonctionnalités



\* \*\*Analyse Matérielle Automatique :\*\* Détection automatique du modèle de processeur, de la quantité de mémoire RAM et des cartes graphiques via des requêtes WMI\[cite: 1].

\* \*\*Stress Test Multi-Thread :\*\* 

&#x20; \* \*\*CPU :\*\* Calculs de fonctions trigonométriques et de racines carrées intensifs répartis sur tous les cœurs logiques\[cite: 1].

&#x20; \* \*\*RAM :\*\* Allocation d'un bloc de mémoire statique persistant avec lecture/écriture espacée pour maximiser le trafic sur le bus mémoire tout en évitant de surcharger le Garbage Collector\[cite: 1].

&#x20; \* \*\*GPU :\*\* Rendu 3D continu de formes géométriques animées via un `Viewport3D` WPF pour mesurer le taux de rafraîchissement (FPS) sous charge\[cite: 1, 6].

\* \*\*Deux Modes d'Intensité :\*\*

&#x20; \* \*\*Mode Calme :\*\* Charge standard pour un test de stabilité rapide\[cite: 1, 6].

&#x20; \* \*\*Mode Intense :\*\* Sursouscription des threads (2x plus de threads que de cœurs) et blocs de mémoire agrandis pour saturer au maximum le système\[cite: 1].

\* \*\*Suivi en Direct :\*\* Affichage en temps réel des opérations CPU par seconde, du débit de la RAM en Mo/s, des FPS de la scène 3D et de la température du processeur\[cite: 1, 6].

\* \*\*Système de Score et Historique :\*\* Calcul d'un score global pondéré avec attribution d'une catégorie de performance (\*Faible, Correct, Bon, Très bon, Excellent\*)\[cite: 1]. Sauvegarde locale automatique des 200 derniers tests dans un fichier JSON pour suivre l'évolution des scores\[cite: 3, 4].

\* \*\*Export des résultats :\*\* Copie textuelle du verdict dans le presse-papier ou capture d'écran graphique du panneau des scores au format PNG\[cite: 1, 6].



\---



\## 🛠️ Technologies utilisées



\* \*\*Framework :\*\* .NET 8.0 (WPF)\[cite: 7]

\* \*\*Langage :\*\* C# 12

\* \*\*Dépendance système :\*\* `System.Management` (pour les requêtes WMI sur le matériel)\[cite: 7]

\* \*\*Format de données :\*\* `System.Text.Json` (pour l'historique des scores)\[cite: 3]



\---



\## 📦 Comment compiler et lancer l'application



\### Prérequis

\* Un système d'exploitation Windows.

\* Le SDK .NET 8.0 installé (si tu souhaites le recompiler toi-même).



\### Option 1 : Générer un exécutable unique (Single File)

Pour compiler l'application sous la forme d'un seul fichier `.exe` autonome (qui inclut le framework .NET pour fonctionner sur n'importe quel PC), ouvre un terminal dans le dossier racine et tape :



```bash

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true

