<div align="center">

# ⚡ PC Stress Benchmark ⚡
**L'outil ultime pour tester la puissance et la stabilité de ton PC**

![.NET 8](https://img.shields.io/badge/.NET%208.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Platform](https://img.shields.io/badge/Plateforme-Windows%20x64-blue?style=for-the-badge)

*Un benchmark complet, fluide et moderne développé en C# pour pousser le CPU, la RAM et le GPU dans leurs derniers retranchements.*

---

</div>

## 📌 À propos du projet

**PC Stress Benchmark** est une application lourde conçue avec **WPF** et **.NET 8.0**[cite: 7]. Elle permet d'évaluer les performances réelles d'un ordinateur en appliquant une charge calculée sur les composants clés, tout en surveillant les métriques système en direct (température, débits, FPS).

> 💡 **Pourquoi l'utiliser ?**  
> Idéal pour vérifier la stabilité d'un PC après un overclocking, tester l'efficacité d'un refroidissement, ou simplement comparer les performances de ta machine avec tes potes ![cite: 1]

---

## 🔥 Fonctionnalités Principales

| Composant | Méthode de Test | Métrique en direct |
| :--- | :--- | :--- |
| 🧠 **Processeur (CPU)** | Calculs flottants intensifs & trigonométrie sur tous les cœurs logiques[cite: 1] | `Opérations / seconde`[cite: 1] |
| 💾 **Mémoire (RAM)** | Allocation de blocs lourds avec stress du bus mémoire en lecture/écriture[cite: 1] | `Débit en Mo/s`[cite: 1] |
| 🎮 **Graphismes (GPU)** | Rendu 3D continu et animations géométriques en temps réel (*Viewport3D*)[cite: 1, 6] | `Images / seconde (FPS)`[cite: 1] |
| 🌡️ **Moniteur WMI** | Capteur de température intégré via les sondes ACPI de la carte mère[cite: 1] | `Température en °C`[cite: 1] |

---

## ⚙️ Les Modes d'Intensité

L'application propose deux modes de test adaptés à ta configuration[cite: 1, 6] :

* 🟢 **Mode Calme :** Charge standard pour un test de stabilité rapide (15s à 120s) sans bloquer le reste de ton système[cite: 1, 6].
* 🔴 **Mode Intense :** **Sursouscription des threads** (2x plus de threads que de cœurs physiques) et blocs RAM massifs[cite: 1]. *Attention : Conçu pour faire transpirer les PC puissants !*[cite: 1, 6]

---

## 🏆 Système de Score & Rangs

À la fin de chaque benchmark, un **score global pondéré** est généré, accompagné d'un badge de performance et d'un verdict sur les capacités de la machine[cite: 1, 6] :

| Score | Rang | Usages recommandés |
| :---: | :---: | :--- |
| **< 500** | 🔴 *Faible* | Bureautique légère, navigation web, mail[cite: 1]. |
| **500 - 1 499** | 🟠 *Correct* | Bureautique avancée, streaming, gaming léger[cite: 1]. |
| **1 500 - 3 999** | 🔵 *Bon* | Machine polyvalente, gaming moyen/haut, multitâche confortable[cite: 1]. |
| **4 000 - 7 999** | 🟢 *Très bon* | Gaming haute qualité, montage vidéo, développement lourd[cite: 1]. |
| **8 000 +** | 🟣 *Excellent* | **PC Master Race** : Gaming Ultra, rendu 3D, 4K sans souci[cite: 1]. |

> 📊 **Historique intégré :** L'application sauvegarde automatiquement tes 200 derniers résultats et trace une courbe d'évolution pour suivre tes performances dans le temps ![cite: 3, 4]

---

## 🚀 Installation & Compilation

### Option 1 : Compilation en un seul fichier (`.exe` autonome)
Pour générer un exécutable unique qui contient déjà tout le framework .NET (pas besoin d'installer .NET sur le PC cible), lance cette commande dans ton terminal :

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true