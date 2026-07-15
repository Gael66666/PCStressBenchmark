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

## 📥 TÉLÉCHARGEMENT & DÉMARRAGE RAPIDE

Tu veux juste tester ton PC sans t'embêter avec le code source ? 
👉 **[Va directement dans la section RELEASES (à droite de la page ou en cliquant ici)](../../releases/latest)** et télécharge le fichier **`PCStressBenchmarkGUI.exe`**. 

Il s'agit d'un exécutable portable et autonome (aucun besoin d'installer .NET ou quoi que ce soit sur ton PC) !

> ### ⚠️ Note importante concernant Windows SmartScreen
> Au tout premier lancement, Windows peut afficher un écran bleu disant : *"Windows a protégé votre ordinateur"*. **C'est 100 % normal !**  
> Comme cet outil est un logiciel open-source indépendant récent, Windows ne le connaît pas encore dans sa base de données mondiale.  
> 🔓 **Pour lancer l'application en 2 secondes :**
> 1. Clique sur le texte souligné **"Plus d'informations"**.
> 2. Un nouveau bouton va apparaître : clique sur **"Exécuter quand même"**.
> 3. *Astuce : Pour une détection optimale de la mémoire RAM et des capteurs de température, fais un clic droit sur le fichier et choisis **"Exécuter en tant qu'administrateur"**.*

---

## 📌 À propos du projet

**PC Stress Benchmark** est une application lourde conçue avec **WPF** et **.NET 8.0**. Elle permet d'évaluer les performances réelles d'un ordinateur en appliquant une charge calculée sur les composants clés, tout en surveillant les métriques système en direct (température, débits, FPS).

> 💡 **Pourquoi l'utiliser ?**  
> Idéal pour vérifier la stabilité d'un PC après un overclocking, tester l'efficacité d'un refroidissement, ou simplement comparer les performances de ta machine avec tes potes !

---

## 🔥 Fonctionnalités Principales

| Composant | Méthode de Test | Métrique en direct |
| :--- | :--- | :--- |
| 🧠 **Processeur (CPU)** | Calculs flottants intensifs & trigonométrie sur tous les cœurs logiques | `Opérations / seconde` |
| 💾 **Mémoire (RAM)** | Allocation de blocs lourds avec stress du bus mémoire en lecture/écriture | `Débit en Mo/s` |
| 🎮 **Graphismes (GPU)** | Rendu 3D continu d'animations géométriques en temps réel (*Viewport3D*) | `Images / seconde (FPS)` |
| 🌡️ **Moniteur WMI** | Capteur de température intégré via les sondes ACPI de la carte mère | `Température en °C` |

---

## ⚙️ Les Modes d'Intensité

L'application propose deux modes de test adaptés à ta configuration :

* 🟢 **Mode Calme :** Charge standard pour un test de stabilité rapide (15s à 120s) sans bloquer le reste de ton système.
* 🔴 **Mode Intense :** **Sursouscription des threads** (2x plus de threads que de cœurs physiques) et blocs RAM massifs. *Attention : Conçu pour faire transpirer les PC puissants ! Le bouton "Arrêter" reste fonctionnel à tout moment en cas de besoin.*

---

## 🏆 Système de Score & Rangs

À la fin de chaque benchmark, un **score global pondéré** est généré, accompagné d'un badge de performance et d'un verdict sur les capacités de la machine :

| Score | Rang | Usages recommandés |
| :---: | :---: | :--- |
| **< 500** | 🔴 *Faible* | Bureautique légère, navigation web, mail. |
| **500 - 1 499** | 🟠 *Correct* | Bureautique avancée, streaming, gaming léger. |
| **1 500 - 3 999** | 🔵 *Bon* | Machine polyvalente, gaming moyen/haut, multitâche confortable. |
| **4 000 - 7 999** | 🟢 *Très bon* | Gaming haute qualité, montage vidéo, développement lourd. |
| **8 000 +** | 🟣 *Excellent* | **PC Master Race** : Gaming Ultra, rendu 3D, 4K sans souci. |

> 📊 **Historique intégré :** L'application sauvegarde automatiquement tes 200 derniers résultats et trace une courbe d'évolution pour suivre tes performances dans le temps ! Tu peux aussi copier ton score en un clic ou exporter une capture d'écran `.png` !

---

## 🛠️ Pour les développeurs (Compilation du code source)

Si tu souhaites cloner le projet et modifier le code par toi-même :

```bash
# 1. Cloner le dépôt
git clone [https://github.com/Gael66666/PCStressBenchmark.git](https://github.com/Gael66666/PCStressBenchmark.git)
cd PCStressBenchmark

# 2. Lancer en mode développement
dotnet run

# 3. Générer l'exécutable unique portable (Release win-x64)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true
