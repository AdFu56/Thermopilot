Imports System
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Formulaire principal multi-centrale.
''' Onglets : Connexion | Voies C1..CN | Acquisition | CSV | Relais | Chronogramme
''' </summary>
Public Class FormPrincipal
    Inherits Form

    ' ─── Objets métier ────────────────────────────────────────────────────────

    Private _gestionnaire  As New GestionnaireMultiCentrale()
    Private _acquisition   As New MoteurAcquisition()
    Private _acqDemarreeParChrono As Boolean = False  ' True si l'acquisition a été lancée automatiquement par le chronogramme
    Private _chronogramme  As New MoteurChronogramme()
    Private _historique    As New HistoriqueMultiCentrale(800)
    Private _bibliotheque  As New BibliothequePeripheriques()
    Friend  _config        As New ConfigManager()

    ' ─── Onglets délégués ─────────────────────────────────────────────────────

    Private _ongletConnexion     As New OngletConnexion()
    Private _ongletCalculs       As New OngletCalculs()
    Private _gestCalculs         As New GestionnaireCalculs()
    Private _ongletPeripheriques As New OngletPeripheriques()
    Private _gestionnaireVoies   As New GestionnaireOngletsVoies()
    Private _ongletCSV           As New OngletCSV()
    Private _ongletSysteme       As New OngletSysteme()
    Private _ongletRapport       As New OngletRapport()
    Private _generateurRapport   As New GenerateurRapport()

    ' ─── Panneaux info débit (repliables, dans 4 onglets) ────────────────────
    Private _infoDebitAcq    As New PanneauInfoDebit()
    Private _infoDebitRelais As New PanneauInfoDebit()
    Private _infoDebitChrono As New PanneauInfoDebit()
    Private _infoDebitSys    As New PanneauInfoDebit()

    ' ─── TabControl ───────────────────────────────────────────────────────────

    Private _tabControl       As New TabControl()
    Private _tabConnexion     As New TabPage("🔌 Connexion")
    Private _tabPeripheriques As New TabPage("🔧 Périphériques")
    Private _tabAcquisition   As New TabPage("📊 Acquisition")
    Private _tabCSV           As New TabPage("📄 Fichier CSV")
    Private _tabRelais        As New TabPage("⚡ Relais")
    Private _tabChrono        As New TabPage("⏱ Chronogramme")
    Private _tabSysteme       As New TabPage("🖼 Système")
    Private _tabRapport       As New TabPage("📋 Rapport")
    Private _tabResultats     As New TabPage("📈 Résultats")
    Private _ongletVisuCSV    As New OngletVisuCSV()

    ' ─── Barre de statut ──────────────────────────────────────────────────────

    Private _lblStatut    As New ToolStripStatusLabel()
    Private _lblNbMesures As New ToolStripStatusLabel()
    Private _lblHeure     As New ToolStripStatusLabel()
    Private WithEvents _timerHorloge As New Timer() With {.Interval = 1000}

    ' ─── Onglet Acquisition ───────────────────────────────────────────────────

    Private _dgvMesures     As New DataGridView()
    Private _splitDroit       As New SplitContainer()   ' séparateur tableau / graphique
    Private _btnToggleTableau  As New Button()            ' masquer/afficher le tableau
    Private _btnModeGraphique  As New Button()            ' bascule XY / histogramme
    Private _btnCopieCSV       As New Button()            ' copie CSV temporaire vers onglet Résultats
    Private _splitCalculs     As SplitContainer = Nothing ' séparateur mesures / calculs
    Private _btnToggleCalculs As New Button()            ' masquer/afficher le panneau calculs
    Private _pnlCheckVoies  As New FlowLayoutPanel()   ' cases à cocher voies
    Private _pnlCheckRelais As New FlowLayoutPanel()   ' cases à cocher relais
    Private _btnDemarrerAcq As New Button()
    Private _btnArreterAcq  As New Button()
    Private _numIntervalle       As New NumericUpDown()
    Private _cmbUniteIntervalle  As New ComboBox()
    Private _numFenetre          As New NumericUpDown()  ' durée fenêtre glissante (0=tout)
    Private _cmbUniteFenetre     As New ComboBox()
    Private _menuStrip           As New MenuStrip()
    Private _chkSauvegarder As New CheckBox()
    Private _chkSimulation  As New CheckBox()
    Private _panelGraphique As New PanelGraphique()

    ' ─── Onglet Relais ────────────────────────────────────────────────────────

    Private _pnlRelaisCorps  As New FlowLayoutPanel()
    Private _chkModeManuel   As New CheckBox()
    Private _boutonsRelais    As New Dictionary(Of String, Button)
    Private _boutonsRelaisOff As New Dictionary(Of String, Button)
    Private _labelsRelais     As New Dictionary(Of String, Label)
    ' Sorties verrouillées manuellement (le chronogramme ne les pilote pas)
    Private _sortiesManuel    As New HashSet(Of String)

    ' ─── Onglet Chronogramme ──────────────────────────────────────────────────

    Private _dgvEtapes         As New DataGridView()
    Private _btnAjouterEtape   As New Button()
    Private _btnSupprimerEtape As New Button()
    Private _btnMonterEtape    As New Button()
    Private _btnDescendreEtape As New Button()
    Private _btnDemarrerChrono As New Button()
    Private _btnArreterChrono  As New Button()
    Private _chkArreterAcqFinChrono As New CheckBox()
    Private _numDureeCycle     As New NumericUpDown()
    Private _cmbUniteDuree     As New ComboBox()
    Private _chkBoucler        As New CheckBox()
    Private _lblEtapeCourante  As New Label()
    Private _pbarEtape         As New ProgressBar()
    Private _lblSecu           As New Label()
    Private _btnSauverChrono   As New Button()
    Private _colonnesChronoRelais As New List(Of String)

    ' ─── Constructeur ─────────────────────────────────────────────────────────

    Public Sub New()
        ' Charger le dernier fichier de config utilisé (si existant)
        Dim dernierFichier = LireDernierFichierConfig()
        If dernierFichier <> "" AndAlso IO.File.Exists(dernierFichier) Then
            ConfigManager.CheminFichier = dernierFichier
        End If
        _config.Charger()
        _config.AppliquerDefauts()

        InitializeComponent()
        InitialiserObjetsMetier()
        ConnecterEvenements()

        ' Charger la config connexion et créer le bon nombre de centrales
        _ongletConnexion.ChargerDepuisConfig()

        ' Charger CSV
        _ongletCSV.ChargerDepuisConfig()

        ' Charger rapport
        _ongletRapport.ChargerDepuisConfig()

        ' Charger chronogramme
        ChargerConfigChrono()

        ' Charger les styles graphique via la propriété Config
        _panelGraphique.Config = _config

        ' Charger les voies calculées
        _gestCalculs.ChargerDepuisConfig(_config)
        _ongletCalculs.RemplirGrille()

        _timerHorloge.Start()
    End Sub

    ' ─── Initialisation IHM ──────────────────────────────────────────────────

    Private Sub InitializeComponent()
        Me.Text          = AppInfo.TitreComplet
        Me.Size          = New Size(1400, 900)
        Me.MinimumSize   = New Size(1100, 700)
        Me.StartPosition = FormStartPosition.CenterScreen

        ' Icône de l'application
        Dim cheminIcone = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "ressources", "icone", "icone_TP.png")
        If System.IO.File.Exists(cheminIcone) Then
            Try
                Using bmp As New System.Drawing.Bitmap(cheminIcone)
                    Me.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon())
                End Using
            Catch
                ' Icône non critique — on continue sans
            End Try
        End If

        ' ── Menu Fichier ──────────────────────────────────────────────────────────
        _menuStrip.BackColor = Color.FromArgb(40, 44, 55)
        _menuStrip.ForeColor = Color.White
        Dim mFichier As New ToolStripMenuItem("Fichier") With {.ForeColor = Color.White}
        Dim mNouveau    = New ToolStripMenuItem("Nouveau",             Nothing, AddressOf Menu_Nouveau)
        Dim mOuvrir     = New ToolStripMenuItem("Ouvrir config...",    Nothing, AddressOf Menu_Ouvrir)
        Dim mSauver     = New ToolStripMenuItem("Enregistrer",         Nothing, AddressOf Menu_Sauver)
        Dim mSauverSous = New ToolStripMenuItem("Enregistrer sous...", Nothing, AddressOf Menu_SauverSous)
        Dim mSep        = New ToolStripSeparator()
        Dim mQuitter    = New ToolStripMenuItem("Quitter", Nothing, Sub(s, e) Me.Close())
        mFichier.DropDownItems.AddRange({mNouveau, mOuvrir, mSauver, mSauverSous, mSep, mQuitter})
        _menuStrip.Items.Add(mFichier)
        Me.MainMenuStrip = _menuStrip

        Dim sb As New StatusStrip()
        _lblStatut.Text    = "Aucune centrale connectée"
        _lblStatut.Spring  = False
        _lblNbMesures.Text = "Mesures : 0"
        _lblHeure.Text     = DateTime.Now.ToString("HH:mm:ss")
        _lblHeure.BorderSides = ToolStripStatusLabelBorderSides.Left
        Dim lblVersion As New ToolStripStatusLabel() With {
            .Text      = AppInfo.TitreCourt,
            .ForeColor = Color.FromArgb(120, 130, 150),
            .Font      = New Font("Segoe UI", 7.5, FontStyle.Italic),
            .Alignment = ToolStripItemAlignment.Right,
            .BorderSides = ToolStripStatusLabelBorderSides.Left
        }
        sb.Items.AddRange({
            _lblStatut,
            New ToolStripStatusLabel() With {.Spring = True},
            _lblNbMesures,
            lblVersion,
            _lblHeure
        })
        Me.Controls.Add(sb)

        _tabControl.Dock = DockStyle.Fill
        _tabControl.Font = New Font("Segoe UI", 9.5)
        ' Onglets fixes (les onglets Voies seront insérés après Périphériques)
        _tabControl.Controls.AddRange({
            _tabConnexion, _tabPeripheriques, _tabAcquisition, _tabCSV, _tabRelais, _tabChrono, _tabSysteme, _tabRapport, _tabResultats
        })
        Me.Controls.Add(_tabControl)
        Me.Controls.Add(_menuStrip)

        _tabConnexion.Controls.Add(_ongletConnexion.ConstruirePanel())
        _tabPeripheriques.Controls.Add(_ongletPeripheriques.ConstruirePanel())
        ConstruireOngletAcquisition()
        _tabCSV.Controls.Add(_ongletCSV.ConstruirePanel())
        ConstruireOngletRelais()
        ConstruireOngletChronogramme()
        _tabSysteme.Controls.Add(_ongletSysteme.ConstruirePanel())
        _tabRapport.Padding = New Padding(0, 0, 0, 30)
        _tabRapport.Controls.Add(_ongletRapport.ConstruirePanel())
        _tabRapport.Controls.Add(_ongletRapport.ConstruirePanneauSauvegarde())
        _ongletVisuCSV.Config        = _config
        _ongletVisuCSV.DossierDefaut = _ongletCSV.DossierCSV
        _tabResultats.Controls.Add(_ongletVisuCSV.ConstruirePanel())

        ' Onglet Calculs — inséré dans l'onglet Acquisition comme sous-onglet
        ConstruireSousOngletCalculs()
    End Sub

    ' ─── Objets métier ────────────────────────────────────────────────────────

    Private Sub InitialiserObjetsMetier()
        ' Bibliothèque périphériques
        _ongletPeripheriques.Config      = _config
        _ongletPeripheriques.Bibliotheque = _bibliotheque

        _ongletConnexion.Gestionnaire = _gestionnaire
        _ongletConnexion.Config       = _config

        _gestionnaireVoies.Config              = _config
        _gestionnaireVoies.Gestionnaire        = _gestionnaire
        _gestionnaireVoies.Bibliotheque        = _bibliotheque
        _gestionnaireVoies.TabControlPrincipal = _tabControl
        _gestionnaireVoies.IndexDepart         = 2   ' après Connexion + Périphériques

        _ongletCSV.Config       = _config
        _ongletCSV.Gestionnaire = _gestionnaire
        _ongletCSV.GestCalculs  = _gestCalculs

        _generateurRapport.Gestionnaire = _gestionnaire
        _generateurRapport.GestCalculs  = _gestCalculs
        _generateurRapport.Acquisition  = _acquisition
        _generateurRapport.OngletCSV    = _ongletCSV
        _ongletRapport.Config     = _config
        _ongletRapport.Generateur = _generateurRapport
        _ongletCSV.GestionVoies = Nothing

        _acquisition.Gestionnaire        = _gestionnaire
        _acquisition.FormulairePrincipal = Me

        _chronogramme.Gestionnaire = _gestionnaire

        _ongletSysteme.Config       = _config
        _ongletSysteme.Gestionnaire = _gestionnaire
        _ongletSysteme.GestCalculs  = _gestCalculs
        AddHandler _ongletSysteme.StatutChange,        Sub(s, msg, err) AfficherStatut(msg, err)
        AddHandler _ongletSysteme.DemandeNotification, Sub(s) BtnNotification_Click(s, EventArgs.Empty)

        ' Calculs utilisateur
        _gestionnaire.GestCalculs = _gestCalculs
        _ongletCalculs.Config       = _config
        _ongletCalculs.Gestionnaire = _gestionnaire
        _ongletCalculs.GestCalculs  = _gestCalculs
        _ongletCalculs.Historique   = _historique
        AddHandler _ongletCalculs.StatutChange,    Sub(s, msg, err) AfficherStatut(msg, err)
        AddHandler _ongletCalculs.CalculsModifies, AddressOf OnCalculsModifies
    End Sub

    ' ─── Connexion des événements ─────────────────────────────────────────────

    Private Sub ConnecterEvenements()
        ' Périphériques → mettre à jour les listes dans tous les onglets Centrale
        AddHandler _ongletPeripheriques.BibliothequeModifiee, AddressOf OnBibliothequeModifiee
        AddHandler _ongletPeripheriques.StatutChange,         Sub(s, msg, err) AfficherStatut(msg, err)
        ' Connexion
        AddHandler _ongletConnexion.NbCentralesChange, AddressOf OnNbCentralesChange
        AddHandler _ongletConnexion.ConnexionEtablie,  AddressOf OnConnexionEtablie
        AddHandler _ongletConnexion.ConnexionFermee,   AddressOf OnConnexionFermee
        AddHandler _ongletConnexion.StatutChange,      Sub(s, msg, err) AfficherStatut(msg, err)

        ' Voies
        AddHandler _gestionnaireVoies.VoiesAppliquees, AddressOf OnVoiesAppliquees
        AddHandler _gestionnaireVoies.StatutChange,    Sub(s, msg, err) AfficherStatut(msg, err)

        ' CSV
        AddHandler _ongletCSV.StatutChange, Sub(s, msg, err) AfficherStatut(msg, err)
        AddHandler _ongletVisuCSV.StatutChange,
            Sub(s As Object, msg As String, err As Boolean)
                If Me.InvokeRequired Then
                    Me.BeginInvoke(Sub() AfficherStatut(msg, err))
                Else
                    AfficherStatut(msg, err)
                End If
            End Sub

        ' Acquisition
        AddHandler _btnDemarrerAcq.Click, AddressOf BtnDemarrerAcq_Click
        AddHandler _btnArreterAcq.Click,  AddressOf BtnArreterAcq_Click
        AddHandler _acquisition.NouvellesMesures,  AddressOf Acq_NouvellesMesures
        AddHandler _acquisition.ErreurAcquisition, Sub(s, msg) AfficherStatut("⚠ " & msg, True)

        ' Relais — en mode manuel, suspendre le pilotage du chronogramme
        AddHandler _chkModeManuel.CheckedChanged, Sub(s, e)
            _chronogramme.ModeManuelActif = _chkModeManuel.Checked
            ActualiserDispoRelais()
        End Sub

        ' Chronogramme
        AddHandler _btnAjouterEtape.Click,    AddressOf BtnAjouterEtape_Click
        AddHandler _btnSupprimerEtape.Click,  AddressOf BtnSupprimerEtape_Click
        AddHandler _btnMonterEtape.Click,     Sub(s, e) EchangerEtape(-1)
        AddHandler _btnDescendreEtape.Click,  Sub(s, e) EchangerEtape(1)
        AddHandler _btnDemarrerChrono.Click,  AddressOf BtnDemarrerChrono_Click
        AddHandler _btnArreterChrono.Click,   AddressOf BtnArreterChrono_Click
        AddHandler _btnSauverChrono.Click,    AddressOf BtnSauverChrono_Click
        AddHandler _chronogramme.EtapeChange, Sub(s, nom, idx)
            BeginInvoke(Sub()
                _lblEtapeCourante.Text = String.Format("Étape {0}/{1} : {2}",
                    idx + 1, _chronogramme.Etapes.Count, nom)
            End Sub)
        End Sub
        AddHandler _chronogramme.SecuriteDeclenche, Sub(s, msg)
            BeginInvoke(Sub() _lblSecu.Text = "⚠ " & msg)
        End Sub
        AddHandler _chronogramme.ChronogrammeTermine,
            Sub(s) BeginInvoke(Sub()
                AfficherStatut("Chronogramme terminé.")
                If _chkArreterAcqFinChrono.Checked Then
                    _acquisition.Arreter()
                    _btnDemarrerAcq.Enabled = True
                    _btnArreterAcq.Enabled  = False
                    ExporterGraphiquePourRapport()
                    AfficherStatut("Chronogramme terminé — acquisition arrêtée.")
                Else
                    ExporterGraphiquePourRapport()
                End If
            End Sub)

        ' Rafraîchir les indicateurs relais (onglets Acquisition + Relais)
        ' à chaque changement d'état piloté par le chronogramme
        AddHandler _chronogramme.EtatChange, Sub(s As Object, args As EtatChronogrammeEventArgs)
            BeginInvoke(Sub() ActualiserIndicateursRelais())
        End Sub

        AddHandler _timerHorloge.Tick, Sub(s, e) _lblHeure.Text = DateTime.Now.ToString("HH:mm:ss")
    End Sub

    ' ─── Bibliothèque périphériques modifiée ─────────────────────────────────

    Private Sub OnBibliothequeModifiee(sender As Object)
        ' Propager la liste mise à jour à tous les onglets Centrale
        _gestionnaireVoies.PropagerBibliotheque(_bibliotheque)
        AfficherStatut("Bibliothèque de périphériques mise à jour — " &
                       _bibliotheque.Items.Count & " périphérique(s).")
    End Sub

    ' ─── Réponse au changement du nombre de centrales ─────────────────────────

    Private Sub OnNbCentralesChange(sender As Object, nb As Integer)
        ' Reconstruire les onglets Voies
        _gestionnaireVoies.ReconstruireOnglets(nb)

        ' Réajuster : Connexion, Périphériques, Voies C1..CN, puis les onglets fixes
        Dim tabsFixesApres As New List(Of TabPage) From {
            _tabAcquisition, _tabCSV, _tabRelais, _tabChrono, _tabSysteme, _tabRapport, _tabResultats
        }
        For Each t In tabsFixesApres
            _tabControl.TabPages.Remove(t)
        Next
        For Each t In tabsFixesApres
            _tabControl.TabPages.Add(t)
        Next

        AfficherStatut(nb.ToString() & " centrale(s) configurée(s).")
    End Sub

    Private Sub OnConnexionEtablie(sender As Object, centrale As CentraleKeithley)
        AfficherStatut(String.Format("Centrale {0} connectée ({1})",
            centrale.NomAffiche, centrale.IPAddress))
        ' Mettre à jour le nom de l'onglet Voies
        _gestionnaireVoies.MettreAJourNomOnglet(centrale.Numero)
    End Sub

    Private Sub OnConnexionFermee(sender As Object, centrale As CentraleKeithley)
        _acquisition.Arreter()
        _chronogramme.Arreter()
        AfficherStatut("Centrale " & centrale.NomAffiche & " déconnectée.")
    End Sub

    ' ─── Voies appliquées ────────────────────────────────────────────────────

    Private Sub OnVoiesAppliquees(sender As Object, centrale As CentraleKeithley)
        ' Mettre à jour la grille Acquisition
        ActualiserGrilleMesures()

        ' Mettre à jour les cases à cocher du graphique
        ActualiserCasesGraphique()

        ' Actualiser la liste d'insertion dans l'onglet Calculs
        _ongletCalculs.ActualiserListeVoies()

        ' Reconstruire l'onglet Relais
        ReconstruireRelais()

        ' Mettre à jour les colonnes du chronogramme
        ReconstruireColonnesChronogramme()

        ' Mettre à jour les relais dynamiques du chronogramme
        _chronogramme.MettreAJourRelaisDynamiques()
        ActualiserListesRegles()
        _ongletSysteme.ActualiserListeDepuisGestionnaire()
        ActualiserPanneauxInfoDebit()
        _ongletCSV.MettreAJourApercu()

        ' Vider l'historique (nouvelles voies)
        _historique.Vider()
    End Sub

    ' ─── ONGLET ACQUISITION ───────────────────────────────────────────────────

    Private Sub ConstruireOngletAcquisition()
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' Barre d'outils
        Dim tb As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Top,
            .AutoSize      = True,
            .AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            .Padding       = New Padding(6, 6, 6, 6),
            .WrapContents  = False
        }
        _btnDemarrerAcq.Text      = "▶ Démarrer"
        _btnDemarrerAcq.BackColor = Color.FromArgb(55, 140, 60)
        _btnDemarrerAcq.ForeColor = Color.White
        _btnDemarrerAcq.FlatStyle = FlatStyle.Flat
        _btnDemarrerAcq.Width     = 110
        _btnDemarrerAcq.Height    = 28
        _btnDemarrerAcq.Margin    = New Padding(0, 0, 6, 0)

        _btnArreterAcq.Text      = "■ Arrêter"
        _btnArreterAcq.BackColor = Color.FromArgb(160, 50, 40)
        _btnArreterAcq.ForeColor = Color.White
        _btnArreterAcq.FlatStyle = FlatStyle.Flat
        _btnArreterAcq.Width     = 90
        _btnArreterAcq.Height    = 28
        _btnArreterAcq.Enabled   = False
        _btnArreterAcq.Margin    = New Padding(0, 0, 6, 0)

        Dim lblInt As New Label() With {
            .Text = "Intervalle :", .AutoSize = True, .Margin = New Padding(10, 7, 2, 0)
        }
        _numIntervalle.Minimum = 1
        _numIntervalle.Maximum = 9999
        _numIntervalle.Value   = 5
        _numIntervalle.Width   = 65
        _numIntervalle.Height  = 24
        _numIntervalle.Margin  = New Padding(0, 2, 2, 0)
        _cmbUniteIntervalle.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbUniteIntervalle.Width  = 60 : _cmbUniteIntervalle.Height = 24
        _cmbUniteIntervalle.Margin = New Padding(0, 2, 8, 0)
        _cmbUniteIntervalle.Items.AddRange({"[s]", "[min]", "[h]"})
        _cmbUniteIntervalle.SelectedIndex = 0

        _chkSauvegarder.Text    = "CSV"
        _chkSauvegarder.Checked = True
        _chkSauvegarder.Margin  = New Padding(8, 4, 0, 0)

        Dim btnGoCsv As New Button() With {
            .Text      = "→ Config CSV",
            .Width     = 100,
            .Height    = 28,
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(45, 50, 65),
            .ForeColor = Color.Silver,
            .Margin    = New Padding(8, 0, 0, 0)
        }
        AddHandler btnGoCsv.Click, Sub(s, e) _tabControl.SelectedTab = _tabCSV

        _chkSimulation.Text   = "Simulation"
        _chkSimulation.Margin = New Padding(12, 6, 0, 0)

        ' Fenêtre glissante
        Dim lblFen As New Label() With {
            .Text    = "Fenêtre :",
            .AutoSize = True,
            .Margin  = New Padding(16, 7, 2, 0)
        }
        _numFenetre.Minimum  = 0
        _numFenetre.Maximum  = 9999
        _numFenetre.Value    = 0
        _numFenetre.Width    = 65
        _numFenetre.Height   = 24
        _numFenetre.Margin   = New Padding(0, 2, 2, 0)
        _numFenetre.Increment = 10
        _cmbUniteFenetre.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbUniteFenetre.Width  = 60 : _cmbUniteFenetre.Height = 24
        _cmbUniteFenetre.Margin = New Padding(0, 2, 4, 0)
        _cmbUniteFenetre.Items.AddRange({"[s]", "[min]", "[h]"})
        _cmbUniteFenetre.SelectedIndex = 1
        Dim tt As New ToolTip()
        tt.SetToolTip(_numFenetre, "Durée de la fenêtre glissante en secondes." & vbCrLf &
                                   "0 = afficher tout l'historique.")
        tt.SetToolTip(lblFen,     "Durée de la fenêtre glissante en secondes." & vbCrLf &
                                   "0 = afficher tout l'historique.")
        AddHandler _numFenetre.ValueChanged, Sub(s, e) MettreAJourFenetre()
        AddHandler _cmbUniteFenetre.SelectedIndexChanged, Sub(s, e) MettreAJourFenetre()

        _btnToggleTableau.Text      = "⊞ Masquer tableau"
        _btnToggleTableau.BackColor = Color.FromArgb(55, 60, 80)
        _btnToggleTableau.ForeColor = Color.White
        _btnToggleTableau.FlatStyle = FlatStyle.Flat
        _btnToggleTableau.Width     = 140
        _btnToggleTableau.Height    = 28
        _btnToggleTableau.Margin    = New Padding(8, 0, 0, 0)
        AddHandler _btnToggleTableau.Click, Sub(s, e)
            _splitDroit.Panel1Collapsed = Not _splitDroit.Panel1Collapsed
            _btnToggleTableau.Text = If(_splitDroit.Panel1Collapsed,
                "⊟ Afficher tableau", "⊞ Masquer tableau")
        End Sub

        _btnModeGraphique.Text      = "📊 Histogramme"
        _btnModeGraphique.BackColor = Color.FromArgb(60, 50, 90)
        _btnModeGraphique.ForeColor = Color.White
        _btnModeGraphique.FlatStyle = FlatStyle.Flat
        _btnModeGraphique.Width     = 115
        _btnModeGraphique.Height    = 28
        _btnModeGraphique.Margin    = New Padding(8, 0, 0, 0)
        AddHandler _btnModeGraphique.Click, Sub(s, e)
            If _panelGraphique.Mode = ModeGraphique.SeriesTemporelles Then
                _panelGraphique.Mode = ModeGraphique.Histogramme
                _btnModeGraphique.Text = "📈 Courbes XY"
            Else
                _panelGraphique.Mode = ModeGraphique.SeriesTemporelles
                _btnModeGraphique.Text = "📊 Histogramme"
            End If
            _panelGraphique.MettreAJour(_historique)
        End Sub

        _btnToggleCalculs.Text      = "🧮 Afficher calculs"
        _btnToggleCalculs.BackColor = Color.FromArgb(40, 65, 100)
        _btnToggleCalculs.ForeColor = Color.White
        _btnToggleCalculs.FlatStyle = FlatStyle.Flat
        _btnToggleCalculs.Width     = 145
        _btnToggleCalculs.Height    = 28
        _btnToggleCalculs.Margin    = New Padding(8, 0, 0, 0)
        AddHandler _btnToggleCalculs.Click, Sub(s, e)
            If _splitCalculs Is Nothing Then Return
            _splitCalculs.Panel2Collapsed = Not _splitCalculs.Panel2Collapsed
            _btnToggleCalculs.Text = If(_splitCalculs.Panel2Collapsed,
                "🧮 Afficher calculs", "🧮 Masquer calculs")
        End Sub

        ' Bouton Copie CSV temporaire — visible seulement pendant acquisition CSV
        _btnCopieCSV.Text      = "📋 Copie CSV"
        _btnCopieCSV.BackColor = Color.FromArgb(80, 100, 50)
        _btnCopieCSV.ForeColor = Color.White
        _btnCopieCSV.FlatStyle = FlatStyle.Flat
        _btnCopieCSV.Width     = 105
        _btnCopieCSV.Height    = 28
        _btnCopieCSV.Margin    = New Padding(8, 0, 0, 0)
        _btnCopieCSV.Visible   = False
        AddHandler _btnCopieCSV.Click, AddressOf BtnCopieCSV_Click

        tb.Controls.AddRange({
            _btnDemarrerAcq, _btnArreterAcq,
            lblInt, _numIntervalle, _cmbUniteIntervalle,
            _chkSauvegarder, btnGoCsv, _chkSimulation,
            lblFen, _numFenetre, _cmbUniteFenetre,
            _btnToggleTableau, _btnToggleCalculs,
            _btnModeGraphique, _btnCopieCSV, ConstruireBoutonNotification()
        })

        ' Corps : 3 colonnes — liste voies/relais | grille valeurs | graphique
        Dim splitMain As New SplitContainer()
        splitMain.Dock = DockStyle.Fill

        ' Panneau gauche : TableLayoutPanel 7 lignes
        ' Panneau gauche : SplitContainer vertical entre voies et relais
        Dim splitGauche As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Horizontal,
            .Panel1MinSize = 80, .Panel2MinSize = 80}
        AddHandler splitGauche.HandleCreated, Sub(sc, ev)
            Try : splitGauche.SplitterDistance = CInt(splitGauche.Height * 0.65) : Catch : End Try
        End Sub

        ' Panel1 : VOIES À TRACER (label → boutons → liste)
        Dim lblV As New Label() With {
            .Text = "VOIES À TRACER", .Dock = DockStyle.Top, .Height = 22,
            .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190), .Padding = New Padding(4, 4, 0, 0)}
        Dim pnlBV As New Panel() With {.Dock = DockStyle.Top, .Height = 28}
        Dim btnTV As New Button() With {.Text = "Tout",  .Width = 50, .Height = 24, .Left = 4,  .Top = 2, .FlatStyle = FlatStyle.Flat}
        Dim btnAV As New Button() With {.Text = "Aucun", .Width = 55, .Height = 24, .Left = 58, .Top = 2, .FlatStyle = FlatStyle.Flat}
        AddHandler btnTV.Click, Sub(s, e)
            For Each chkV In _pnlCheckVoies.Controls.OfType(Of CheckBox)()
                chkV.Checked = True
            Next
        End Sub
        AddHandler btnAV.Click, Sub(s, e)
            For Each chkV In _pnlCheckVoies.Controls.OfType(Of CheckBox)()
                chkV.Checked = False
            Next
        End Sub
        pnlBV.Controls.AddRange({btnTV, btnAV})
        _pnlCheckVoies.Dock = DockStyle.Fill
        _pnlCheckVoies.AutoScroll = True
        _pnlCheckVoies.FlowDirection = FlowDirection.TopDown
        _pnlCheckVoies.WrapContents = False
        _pnlCheckVoies.Padding = New Padding(4)
        splitGauche.Panel1.Controls.Add(_pnlCheckVoies)  ' Fill
        splitGauche.Panel1.Controls.Add(pnlBV)            ' Top
        splitGauche.Panel1.Controls.Add(lblV)             ' Top

        ' Panel2 : SORTIES ANALOGIQUES (label → boutons → liste)
        Dim lblR As New Label() With {
            .Text = "SORTIES ANALOGIQUES", .Dock = DockStyle.Top, .Height = 22,
            .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(160, 80, 30),
            .BackColor = Color.FromArgb(252, 244, 240), .Padding = New Padding(4, 4, 0, 0)}
        Dim pnlBR As New Panel() With {.Dock = DockStyle.Top, .Height = 28}
        Dim btnTR As New Button() With {.Text = "Tout",  .Width = 50, .Height = 24, .Left = 4,  .Top = 2, .FlatStyle = FlatStyle.Flat}
        Dim btnAR As New Button() With {.Text = "Aucun", .Width = 55, .Height = 24, .Left = 58, .Top = 2, .FlatStyle = FlatStyle.Flat}
        AddHandler btnTR.Click, Sub(s, e)
            For Each chkR In _pnlCheckRelais.Controls.OfType(Of CheckBox)()
                chkR.Checked = True
            Next
        End Sub
        AddHandler btnAR.Click, Sub(s, e)
            For Each chkR In _pnlCheckRelais.Controls.OfType(Of CheckBox)()
                chkR.Checked = False
            Next
        End Sub
        pnlBR.Controls.AddRange({btnTR, btnAR})
        _pnlCheckRelais.Dock = DockStyle.Fill
        _pnlCheckRelais.AutoScroll = True
        _pnlCheckRelais.FlowDirection = FlowDirection.TopDown
        _pnlCheckRelais.WrapContents = False
        _pnlCheckRelais.Padding = New Padding(4)
        splitGauche.Panel2.Controls.Add(_pnlCheckRelais)  ' Fill
        splitGauche.Panel2.Controls.Add(pnlBR)             ' Top
        splitGauche.Panel2.Controls.Add(lblR)              ' Top

        splitMain.Panel1.Controls.Add(splitGauche)

        ' Panneau droit : grille + graphique
        _splitDroit.Dock        = DockStyle.Fill
        _splitDroit.Orientation = Orientation.Horizontal

        _dgvMesures.Dock                  = DockStyle.Fill
        _dgvMesures.AllowUserToAddRows    = False
        _dgvMesures.ReadOnly              = True
        _dgvMesures.RowHeadersVisible     = False
        _dgvMesures.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvMesures.BackgroundColor       = Color.White
        _dgvMesures.Font                  = New Font("Consolas", 9)
        _dgvMesures.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgvMesures.Columns.Add("colCentrale", "Centrale")
        _dgvMesures.Columns.Add("colVoie",     "Voie")
        _dgvMesures.Columns.Add("colNom",      "Désignation")
        _dgvMesures.Columns.Add("colValeur",   "Valeur")
        _dgvMesures.Columns.Add("colUnite",    "Unité")
        _dgvMesures.Columns("colValeur").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        _dgvMesures.Columns("colValeur").DefaultCellStyle.Font = New Font("Consolas", 10, FontStyle.Bold)
        _dgvMesures.Columns("colCentrale").Width = 90

        _panelGraphique.Dock = DockStyle.Fill

        _splitDroit.Panel1.Controls.Add(_dgvMesures)
        _splitDroit.Panel2.Controls.Add(_panelGraphique)

        splitMain.Panel2.Controls.Add(_splitDroit)

        pnl.Controls.Add(splitMain)
        pnl.Controls.Add(tb)
        _tabAcquisition.Controls.Add(pnl)
    End Sub

    ''' <summary>
    ''' Ajoute un sous-onglet "Calculs" dans l'onglet Acquisition.
    ''' Placé dans un TabControl interne à _tabAcquisition.
    ''' </summary>
    ''' <summary>
    ''' Ajoute un SplitContainer horizontal dans l'onglet Acquisition :
    ''' Panel1 = contenu existant (mesures + graphique)
    ''' Panel2 = onglet Calculs utilisateur
    ''' </summary>
    Private Sub ConstruireSousOngletCalculs()
        Dim controleExistant As Control = Nothing
        If _tabAcquisition.Controls.Count > 0 Then
            controleExistant = _tabAcquisition.Controls(0)
            _tabAcquisition.Controls.Clear()
        End If

        ' SplitContainer : définir MinSize et SplitterDistance APRÈS ajout au parent
        Dim split As New SplitContainer() With {
            .Dock        = DockStyle.Fill,
            .Orientation = Orientation.Horizontal
        }

        If controleExistant IsNot Nothing Then
            controleExistant.Dock = DockStyle.Fill
            split.Panel1.Controls.Add(controleExistant)
        End If

        Dim tabCalculs As New TabControl() With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 8.5)
        }
        Dim page As New TabPage("🧮 Calculs utilisateur")
        page.Controls.Add(_ongletCalculs.ConstruirePanel())
        tabCalculs.TabPages.Add(page)
        split.Panel2.Controls.Add(tabCalculs)

        ' Ajouter au parent EN PREMIER — le contrôle a maintenant une taille réelle
        _tabAcquisition.Controls.Add(split)
        _splitCalculs = split   ' stocker pour le bouton toggle

        ' Définir les contraintes après ajout
        split.Panel1MinSize = 200
        split.Panel2MinSize = 180
        Try
            split.SplitterDistance = CInt(split.Height * 0.65)
        Catch
        End Try

        ' Démarrer avec le panneau calculs masqué
        split.Panel2Collapsed = True
    End Sub

    ''' <summary>Appelé quand les calculs sont modifiés → reconstruire les cases graphique.</summary>
    Private Sub OnCalculsModifies(sender As Object)
        ActualiserGrilleMesures()
        ActualiserCasesGraphique()
        _ongletSysteme.ActualiserListeDepuisGestionnaire()
        _ongletCalculs.ActualiserListeVoies()
        _ongletCSV.MettreAJourApercu()
    End Sub

    Private Sub ActualiserGrilleMesures()
        _dgvMesures.Rows.Clear()
        For Each c In _gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                _dgvMesures.Rows.Add(c.NomAffiche, v.Numero, v.Nom, "---", v.Unite)
            Next
            For Each s In c.Voies.SortiesActives()
                _dgvMesures.Rows.Add(c.NomAffiche, s.Numero, s.Nom & " ↑", "---", "ON/OFF")
            Next
        Next
        ' Voies calculées
        For Each vc In _gestCalculs.Voies.Where(Function(v) v.Active)
            Dim idx = _dgvMesures.Rows.Add("[Calcul]", "", vc.Nom, "---", vc.Unite)
            _dgvMesures.Rows(idx).DefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255)
        Next
    End Sub

    Private Sub ActualiserCasesGraphique()
        _pnlCheckVoies.Controls.Clear()
        _pnlCheckRelais.Controls.Clear()

        Dim series As New List(Of PanelGraphique.SerieGraphique)

        For Each c In _gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                Dim cle = HistoriqueMultiCentrale.CleVoie(c.Numero, v.Numero)
                Dim sg As New PanelGraphique.SerieGraphique() With {
                    .Cle         = cle,
                    .Nom         = v.Nom,
                    .NomCentrale = c.NomAffiche,
                    .Unite       = v.Unite,
                    .EstBinaire  = False,
                    .Visible     = False   ' masqué par défaut
                }
                series.Add(sg)

                Dim chk As New CheckBox() With {
                    .Text     = String.Format("[{0}] {1}", c.NomAffiche, v.Nom),
                    .Checked  = False,     ' décoché par défaut
                    .AutoSize = True,
                    .Font     = New Font("Segoe UI", 8.5)
                }
                Dim cleCapture = cle
                AddHandler chk.CheckedChanged, Sub(s, e)
                    _panelGraphique.SetVisible(cleCapture, CType(s, CheckBox).Checked)
                End Sub
                _pnlCheckVoies.Controls.Add(chk)
            Next

            For Each sor In c.Voies.SortiesActives()
                Dim cle = HistoriqueMultiCentrale.CleSortie(c.Numero, sor.Numero)
                Dim sg As New PanelGraphique.SerieGraphique() With {
                    .Cle         = cle,
                    .Nom         = sor.Nom,
                    .NomCentrale = c.NomAffiche,
                    .Unite       = "ON/OFF",
                    .EstBinaire  = True,
                    .Visible     = False   ' masqué par défaut
                }
                series.Add(sg)

                Dim chk As New CheckBox() With {
                    .Text      = String.Format("[{0}] {1}", c.NomAffiche, sor.Nom),
                    .Checked   = False,    ' décoché par défaut
                    .AutoSize  = True,
                    .Font      = New Font("Segoe UI", 8.5),
                    .ForeColor = Color.FromArgb(180, 100, 30)
                }
                Dim cleCapture = cle
                AddHandler chk.CheckedChanged, Sub(s, e)
                    _panelGraphique.SetVisible(cleCapture, CType(s, CheckBox).Checked)
                End Sub
                _pnlCheckRelais.Controls.Add(chk)
            Next
        Next

        ' ── Voies calculées ──
        For Each vc In _gestCalculs.Voies.Where(Function(v) v.Active)
            Dim cle = vc.CleHistorique
            Dim sg As New PanelGraphique.SerieGraphique() With {
                .Cle         = cle,
                .Nom         = vc.Nom,
                .NomCentrale = "Calcul",
                .Unite       = vc.Unite,
                .EstBinaire  = False,
                .Visible     = False
            }
            series.Add(sg)

            Dim chk As New CheckBox() With {
                .Text      = String.Format("[Calcul] {0} ({1})", vc.Nom, vc.Unite),
                .Checked   = False,
                .AutoSize  = True,
                .Font      = New Font("Segoe UI", 8.5),
                .ForeColor = Color.FromArgb(80, 160, 220)
            }
            Dim cleCapture = cle
            AddHandler chk.CheckedChanged, Sub(s, e)
                _panelGraphique.SetVisible(cleCapture, CType(s, CheckBox).Checked)
            End Sub
            _pnlCheckVoies.Controls.Add(chk)
        Next

        _panelGraphique.DefinirSeries(series)

        ' Forcer le recalcul de AutoScrollMinSize pour que l'ascenseur
        ' descende jusqu'au dernier élément
        _pnlCheckVoies.AutoScrollMinSize = New Size(0,
            _pnlCheckVoies.Controls.OfType(Of CheckBox)().Sum(Function(c) c.Height + c.Margin.Vertical) + 40)
        _pnlCheckRelais.AutoScrollMinSize = New Size(0,
            _pnlCheckRelais.Controls.OfType(Of CheckBox)().Sum(Function(c) c.Height + c.Margin.Vertical) + 40)
    End Sub

    ' ─── ONGLET RELAIS ───────────────────────────────────────────────────────

    Private Sub ConstruireOngletRelais()
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12)}

        ' Panneau en-tête pour la case à cocher Mode manuel
        Dim pnlEntete As New Panel() With {
            .Dock    = DockStyle.Top,
            .Height  = 36,
            .Padding = New Padding(12, 8, 12, 0)
        }
        _chkModeManuel.Text    = "Mode manuel"
        _chkModeManuel.Font    = New Font("Segoe UI", 10, FontStyle.Bold)
        _chkModeManuel.AutoSize = True
        _chkModeManuel.Dock    = DockStyle.Left
        pnlEntete.Controls.Add(_chkModeManuel)

        Dim btnNotifRelais = ConstruireBoutonNotification()
        btnNotifRelais.Dock   = DockStyle.Right
        btnNotifRelais.Margin = New Padding(0)
        pnlEntete.Controls.Add(btnNotifRelais)

        _pnlRelaisCorps.Dock          = DockStyle.Fill
        _pnlRelaisCorps.FlowDirection = FlowDirection.LeftToRight
        _pnlRelaisCorps.WrapContents  = True
        _pnlRelaisCorps.AutoScroll    = True
        _pnlRelaisCorps.Padding       = New Padding(8, 4, 8, 4)

        Dim lblNote As New Label() With {
            .Text      = "⚠ Les dispositifs de chauffage sont coupés automatiquement si le débit est insuffisant." &
                         "  →  Le seuil se configure dans l'onglet Chronogramme.",
            .ForeColor = Color.DarkOrange,
            .Font      = New Font("Segoe UI", 9, FontStyle.Italic),
            .Dock      = DockStyle.Bottom,
            .Height    = 22
        }

        pnl.Controls.Add(_pnlRelaisCorps)
        pnl.Controls.Add(lblNote)
        pnl.Controls.Add(pnlEntete)
        _tabRelais.Controls.Add(pnl)
    End Sub

    Private Sub ReconstruireRelais()
        _pnlRelaisCorps.Controls.Clear()
        _boutonsRelais.Clear()
        _boutonsRelaisOff.Clear()
        _labelsRelais.Clear()

        Dim sortiesActives = _gestionnaire.ToutesSortiesActives()
        If sortiesActives.Count = 0 Then
            _pnlRelaisCorps.Controls.Add(New Label() With {
                .Text = "Aucune sortie active. Configurez les sorties dans les onglets Voies.",
                .ForeColor = Color.Gray, .Font = New Font("Segoe UI", 9, FontStyle.Italic),
                .AutoSize = True, .Margin = New Padding(10, 20, 0, 0)
            })
            Return
        End If

        For Each item In sortiesActives
            Dim c  = item.Centrale
            Dim s  = item.Sortie
            Dim id = HistoriqueMultiCentrale.CleSortie(c.Numero, s.Numero)

            Dim carte As New Panel() With {
                .Width = 210, .Height = 150, .Margin = New Padding(8),
                .BorderStyle = BorderStyle.FixedSingle, .BackColor = Color.FromArgb(250, 250, 252)
            }
            Dim lblC As New Label() With {
                .Text = c.NomAffiche & " — Sortie " & s.Numero,
                .Font = New Font("Segoe UI", 7.5, FontStyle.Bold),
                .ForeColor = Color.FromArgb(60, 90, 140),
                .Location = New Point(6, 4), .AutoSize = True
            }
            Dim lblN As New Label() With {
                .Text = s.Nom, .Font = New Font("Segoe UI", 10, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleCenter,
                .Location = New Point(0, 20), .Width = 210, .Height = 26
            }

            ' Deux boutons ON et OFF côte à côte
            Dim btnOn As New Button() With {
                .Text = "ON", .Font = New Font("Segoe UI", 10, FontStyle.Bold),
                .BackColor = Color.FromArgb(55, 140, 60), .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Location  = New Point(8, 50), .Size = New Size(90, 34),
                .Enabled   = False
            }
            Dim btnOff As New Button() With {
                .Text = "OFF", .Font = New Font("Segoe UI", 10, FontStyle.Bold),
                .BackColor = Color.FromArgb(160, 50, 40), .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Location  = New Point(108, 50), .Size = New Size(90, 34),
                .Enabled   = False
            }
            Dim lblE As New Label() With {
                .Text = "● ARRÊT", .ForeColor = Color.Gray,
                .Font = New Font("Segoe UI", 9, FontStyle.Bold),
                .TextAlign = ContentAlignment.MiddleCenter,
                .Location = New Point(0, 90), .Width = 210, .Height = 24
            }

            Dim idCapture = id
            Dim sCapture  = s
            Dim cCapture  = c

            AddHandler btnOn.Click,  Sub(sender, e) ActiverSortieManuelle(cCapture, sCapture, True)
            AddHandler btnOff.Click, Sub(sender, e) ActiverSortieManuelle(cCapture, sCapture, False)

            carte.Controls.Add(lblC)
            carte.Controls.Add(lblN)
            carte.Controls.Add(btnOn)
            carte.Controls.Add(btnOff)
            carte.Controls.Add(lblE)

            ' Stocker les deux boutons — on utilise btnOn comme référence principale
            _boutonsRelais(id)       = btnOn
            _boutonsRelaisOff(id)    = btnOff
            _labelsRelais(id)        = lblE
            _pnlRelaisCorps.Controls.Add(carte)
        Next
        ActualiserDispoRelais()
    End Sub

    Private Sub ActualiserDispoRelais()
        Dim actif = _chkModeManuel.Checked
        For Each btn In _boutonsRelais.Values
            btn.Enabled = actif
        Next
        For Each btn In _boutonsRelaisOff.Values
            btn.Enabled = actif
        Next
    End Sub

    Private Sub ActiverSortieManuelle(c As CentraleKeithley, s As SortieAnalogique, activer As Boolean)
        If Not c.EstConnectee AndAlso Not _chkSimulation.Checked Then
            MessageBox.Show("Centrale non connectée.", "Sortie", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Mettre à jour l'état en mémoire EN PREMIER (source de vérité pour l'IHM)
        ' avant tout envoi réseau pour éviter que l'acquisition ne l'écrase
        s.TensionV = If(activer, Math.Max(s.UMax, s.SeuilOnV + 0.1), 0.0)

        ' Envoyer la commande à la centrale
        If c.EstConnectee Then
            If s.Mode = SortieAnalogique.ModePilotage.Analogique Then
                c.Keithley.SetTension(s.Numero, If(activer, s.UMax, 0.0))
            Else
                c.Keithley.SetTension(s.Numero, If(activer, s.UMax, 0.0))
            End If
        End If

        ' Synchroniser le RelaisDynamique du chronogramme
        Dim id = HistoriqueMultiCentrale.CleSortie(c.Numero, s.Numero)
        Dim rd = _chronogramme.RelaisDynamiques.FirstOrDefault(Function(r) r.Id = id)
        If rd IsNot Nothing Then rd.Etat = activer

        ' Rafraîchir l'affichage sur le thread UI
        If InvokeRequired Then
            BeginInvoke(Sub() ActualiserIndicateursRelais())
        Else
            ActualiserIndicateursRelais()
        End If
    End Sub

    Private Sub ActualiserIndicateursRelais()
        For Each item In _gestionnaire.ToutesSortiesActives()
            Dim id    = HistoriqueMultiCentrale.CleSortie(item.Centrale.Numero, item.Sortie.Numero)
            Dim estOn = item.Sortie.EstOn
            If _labelsRelais.ContainsKey(id) Then
                _labelsRelais(id).Text      = If(estOn, "● EN MARCHE", "● ARRÊT")
                _labelsRelais(id).ForeColor = If(estOn, Color.Green, Color.Gray)
            End If
            If _boutonsRelais.ContainsKey(id) Then
                _boutonsRelais(id).BackColor = If(estOn,
                    Color.FromArgb(30, 160, 50), Color.FromArgb(55, 140, 60))
            End If
            If _boutonsRelaisOff.ContainsKey(id) Then
                _boutonsRelaisOff(id).BackColor = If(Not estOn,
                    Color.FromArgb(200, 50, 40), Color.FromArgb(160, 50, 40))
            End If
        Next
    End Sub

    ' ─── ACQUISITION ──────────────────────────────────────────────────────────

    Private Sub BtnDemarrerAcq_Click(sender As Object, e As EventArgs)
        If Not _chkSimulation.Checked AndAlso _gestionnaire.NbConnectees = 0 Then
            MessageBox.Show("Connectez au moins une centrale.", "Acquisition",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _tabControl.SelectedTab = _tabConnexion : Return
        End If

        _acquisition.IntervalleMsec = CInt(ParseurDuree.EnSecondes(
            CInt(_numIntervalle.Value).ToString() &
            ParseurDuree.SuffixeParIndex(_cmbUniteIntervalle.SelectedIndex)) * 1000)
        _acquisition.StockerCSV     = _chkSauvegarder.Checked
        _acquisition.ModeSim        = If(_chkSimulation.Checked,
            MoteurAcquisition.ModeSimulation.MonteeEnTemperature,
            MoteurAcquisition.ModeSimulation.Desactive)
        If _chkSauvegarder.Checked Then
            _acquisition.CheminCSV = _ongletCSV.CheminFige()
            _gestionnaire.FormatCSV          = _ongletCSV.FormatValeur
            _gestionnaire.LibelleUniteDuree  = _ongletCSV.LibelleUniteDuree
            _gestionnaire.DiviseurDuree      = _ongletCSV.DiviseurDuree
            _gestionnaire.HeureDepart        = DateTime.Now
        End If

        _gestCalculs.ResetIntegrations()
        _acquisition.GestCalculs = _gestCalculs
        _acquisition.Historique  = _historique
        If _acquisition.Demarrer() Then
            _btnDemarrerAcq.Enabled = False
            _btnArreterAcq.Enabled  = True
            _btnCopieCSV.Visible    = _chkSauvegarder.Checked  ' visible seulement si CSV actif
            AfficherStatut("Acquisition en cours" &
                If(_chkSimulation.Checked, " [SIMULATION]", ""))
        End If
    End Sub

    Private Sub BtnArreterAcq_Click(sender As Object, e As EventArgs)
        _acquisition.Arreter()
        _btnDemarrerAcq.Enabled = True
        _btnArreterAcq.Enabled  = False
        _btnCopieCSV.Visible    = False
        AfficherStatut("Acquisition arrêtée")
        ExporterGraphiquePourRapport()
    End Sub

    ''' <summary>Copie le CSV en cours d'écriture dans un fichier TEMP_ et l'ouvre dans un nouvel onglet Résultats.</summary>
    Private Sub BtnCopieCSV_Click(sender As Object, e As EventArgs)
        Try
            Dim cheminSrc = _acquisition.CheminCSV
            If String.IsNullOrEmpty(cheminSrc) OrElse Not File.Exists(cheminSrc) Then
                AfficherStatut("Copie CSV : fichier source introuvable.", True) : Return
            End If
            Dim dossier  = Path.GetDirectoryName(cheminSrc)
            Dim nomFich  = Path.GetFileName(cheminSrc)
            Dim cheminDst = Path.Combine(dossier, "TEMP_" & nomFich)
            ' Copier avec autorisation de lecture même si le fichier est ouvert en écriture
            Using src As New FileStream(cheminSrc, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                Using dst As New FileStream(cheminDst, FileMode.Create, FileAccess.Write, FileShare.None)
                    src.CopyTo(dst)
                End Using
            End Using
            ' Ouvrir dans un NOUVEL onglet Résultats
            _tabControl.SelectedTab = _tabResultats
            _ongletVisuCSV.AjouterOnglet(Nothing, EventArgs.Empty)
            _ongletVisuCSV.OuvrirFichier(cheminDst)
            AfficherStatut("Copie CSV ouverte dans l'onglet Résultats : " & Path.GetFileName(cheminDst))
        Catch ex As Exception
            AfficherStatut("Erreur copie CSV : " & ex.Message, True)
        End Try
    End Sub

    ''' <summary>Exporte le graphique en PNG et le référence dans le générateur de rapport.</summary>
    Private Sub ExporterGraphiquePourRapport()
        If Not _ongletRapport.SauverGraphique Then Return
        Try
            Dim dossier = _ongletRapport.DossierRapports
            If String.IsNullOrEmpty(dossier) Then Return
            Directory.CreateDirectory(dossier)
            Dim cheminPNG = Path.Combine(dossier,
                "Graphique_" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".png")
            If _panelGraphique.ExporterPNGSilencieux(cheminPNG, 1400, 700) Then
                _generateurRapport.CheminGraphique = cheminPNG
                AfficherStatut("Graphique sauvegardé → " & Path.GetFileName(cheminPNG))
            End If
        Catch ex As Exception
            AfficherStatut("Export graphique : " & ex.Message, True)
        End Try
    End Sub

    Private Sub Acq_NouvellesMesures(sender As Object,
                                      centrale As CentraleKeithley,
                                      horodatage As DateTime)
        ' AjouterMesuresCentrale et CalculerEtInjecter sont maintenant faits
        ' dans Acquisition.vb avant EcrireCSV (pour un CSV cohérent)
        ' Ici on met à jour la grille et les graphiques uniquement
        _ongletCalculs.ActualiserValeursGrille()
        _acquisition.NombreMesures += 1
        _lblNbMesures.Text = "Mesures : " & _acquisition.NombreMesures

        ' Mettre à jour la grille
        Dim rowIdx = 0
        For Each c In _gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                If rowIdx < _dgvMesures.Rows.Count Then
                    If v.EnErreur OrElse Double.IsNaN(v.Valeur) Then
                        _dgvMesures.Rows(rowIdx).Cells("colValeur").Value = "ERR"
                        _dgvMesures.Rows(rowIdx).DefaultCellStyle.BackColor = Color.MistyRose
                    Else
                        _dgvMesures.Rows(rowIdx).Cells("colValeur").Value =
                            v.Valeur.ToString("F" & _ongletCSV.NbDecimales,
                            System.Globalization.CultureInfo.InvariantCulture)
                        _dgvMesures.Rows(rowIdx).DefaultCellStyle.BackColor = Color.White
                    End If
                End If
                rowIdx += 1
            Next
            For Each s In c.Voies.SortiesActives()
                If rowIdx < _dgvMesures.Rows.Count Then
                    _dgvMesures.Rows(rowIdx).Cells("colValeur").Value = If(s.EstOn, "ON", "OFF")
                    _dgvMesures.Rows(rowIdx).DefaultCellStyle.BackColor =
                        If(s.EstOn, Color.FromArgb(220, 255, 220), Color.White)
                End If
                rowIdx += 1
            Next
        Next

        ActualiserIndicateursRelais()

        ' Mettre à jour les voies calculées dans le tableau
        For Each vc In _gestCalculs.Voies.Where(Function(v) v.Active)
            If rowIdx < _dgvMesures.Rows.Count Then
                If vc.EnErreur OrElse Double.IsNaN(vc.Valeur) Then
                    _dgvMesures.Rows(rowIdx).Cells("colValeur").Value = "ERR"
                    _dgvMesures.Rows(rowIdx).DefaultCellStyle.BackColor = Color.MistyRose
                Else
                    _dgvMesures.Rows(rowIdx).Cells("colValeur").Value =
                        vc.Valeur.ToString("F" & _ongletCSV.NbDecimales,
                        System.Globalization.CultureInfo.InvariantCulture)
                    _dgvMesures.Rows(rowIdx).DefaultCellStyle.BackColor = Color.FromArgb(235, 245, 255)
                End If
            End If
            rowIdx += 1
        Next

        _panelGraphique.MettreAJour(_historique)
        _ongletSysteme.MettreAJourValeurs(_historique)
        _infoDebitAcq.MettreAJour()
        _infoDebitRelais.MettreAJour()
        _infoDebitChrono.MettreAJour()
        _infoDebitSys.MettreAJour()
    End Sub

    ' ─── CHRONOGRAMME ────────────────────────────────────────────────────────

    Private Sub ConstruireOngletChronogramme()
        Dim split As New SplitContainer()
        split.Dock        = DockStyle.Fill
        split.Orientation = Orientation.Horizontal

        ' Grille des étapes
        Dim pnlH As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(4)}
        Dim tbE As New FlowLayoutPanel() With {
            .Dock         = DockStyle.Top,
            .AutoSize     = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding      = New Padding(4, 4, 4, 4)
        }
        _btnAjouterEtape.Text    = "+ Ajouter" : _btnAjouterEtape.Width    = 100 : _btnAjouterEtape.Height    = 28 : _btnAjouterEtape.Margin    = New Padding(0, 0, 6, 0)
        _btnSupprimerEtape.Text  = "✕ Suppr."  : _btnSupprimerEtape.Width  = 80  : _btnSupprimerEtape.Height  = 28 : _btnSupprimerEtape.Margin  = New Padding(0, 0, 6, 0)
        _btnMonterEtape.Text     = "▲"          : _btnMonterEtape.Width     = 36  : _btnMonterEtape.Height     = 28 : _btnMonterEtape.Margin     = New Padding(0, 0, 4, 0)
        _btnDescendreEtape.Text  = "▼"          : _btnDescendreEtape.Width  = 36  : _btnDescendreEtape.Height  = 28 : _btnDescendreEtape.Margin  = New Padding(0, 0, 4, 0)

        Dim btnSauverHaut As New Button() With {
            .Text      = "💾 Sauvegarder",
            .BackColor = Color.FromArgb(60, 65, 80),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Width     = 140,
            .Height    = 28,
            .Margin    = New Padding(8, 0, 0, 0)
        }
        AddHandler btnSauverHaut.Click, AddressOf BtnSauverChrono_Click

        tbE.Controls.AddRange({_btnAjouterEtape, _btnSupprimerEtape,
                                _btnMonterEtape, _btnDescendreEtape,
                                btnSauverHaut,
                                ConstruireBoutonNotification()})

        _dgvEtapes.Dock                  = DockStyle.Fill
        _dgvEtapes.AllowUserToAddRows    = False
        _dgvEtapes.RowHeadersVisible     = False
        _dgvEtapes.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvEtapes.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        ' Colonnes fixes
        _dgvEtapes.Columns.AddRange({
            New DataGridViewTextBoxColumn()  With {.HeaderText = "Nom étape", .Name = "colNom"},
            New DataGridViewTextBoxColumn()  With {.HeaderText = "Durée [unité]", .Name = "colDuree", .Width = 110, .ToolTipText = "Durée de l'étape." & vbCrLf & "Exemples : 120  (secondes par défaut)" & vbCrLf & "          2[min]  1.5[h]  6[j]  500[ms]"}
        })
        ' Les colonnes relais sont ajoutées dynamiquement dans ReconstruireColonnesChronogramme

        pnlH.Controls.Add(_dgvEtapes)
        pnlH.Controls.Add(tbE)

        ' Panneau de contrôle
        Dim pnlB As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .Padding = New Padding(8), .ColumnCount = 2, .RowCount = 4
        }
        pnlB.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55))
        pnlB.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45))

        Dim pnlDur As New FlowLayoutPanel() With {.Dock = DockStyle.Fill}
        pnlDur.Controls.Add(New Label() With {.Text = "Durée du cycle :", .AutoSize = True, .Margin = New Padding(0, 5, 4, 0)})
        _numDureeCycle.Minimum = 1 : _numDureeCycle.Maximum = 99999 : _numDureeCycle.Value = 24 : _numDureeCycle.Width = 80
        _cmbUniteDuree.Items.AddRange({"secondes", "minutes", "heures", "jours"})
        _cmbUniteDuree.SelectedIndex = 2 : _cmbUniteDuree.Width = 90 : _cmbUniteDuree.DropDownStyle = ComboBoxStyle.DropDownList
        _chkBoucler.Text = "Boucler" : _chkBoucler.Checked = True : _chkBoucler.Margin = New Padding(8, 4, 0, 0)
        pnlDur.Controls.AddRange({_numDureeCycle, _cmbUniteDuree, _chkBoucler})

        pnlB.Controls.Add(pnlDur, 0, 0)
        pnlB.SetColumnSpan(pnlDur, 2)

        Dim pnlBtns As New FlowLayoutPanel() With {.Dock = DockStyle.Fill}
        _btnDemarrerChrono.Text      = "▶ Démarrer chronogramme"
        _btnDemarrerChrono.BackColor = Color.FromArgb(55, 140, 60)
        _btnDemarrerChrono.ForeColor = Color.White
        _btnDemarrerChrono.FlatStyle = FlatStyle.Flat
        _btnDemarrerChrono.Width     = 200
        _btnDemarrerChrono.Height    = 28
        _btnArreterChrono.Text      = "■ Arrêter"
        _btnArreterChrono.BackColor = Color.FromArgb(160, 50, 40)
        _btnArreterChrono.ForeColor = Color.White
        _btnArreterChrono.FlatStyle = FlatStyle.Flat
        _btnArreterChrono.Width     = 90
        _btnArreterChrono.Height    = 28
        _btnArreterChrono.Enabled   = False
        _chkArreterAcqFinChrono.Text    = "Arrêter l'acquisition à la fin du chronogramme"
        _chkArreterAcqFinChrono.Checked = False
        _chkArreterAcqFinChrono.AutoSize = True
        _chkArreterAcqFinChrono.Margin  = New Padding(16, 6, 0, 0)
        _chkArreterAcqFinChrono.Font    = New Font("Segoe UI", 9)
        pnlBtns.Controls.AddRange({_btnDemarrerChrono, _btnArreterChrono, _chkArreterAcqFinChrono})
        pnlB.Controls.Add(pnlBtns, 0, 1)

        _lblEtapeCourante.Text = "En attente…" : _lblEtapeCourante.Font = New Font("Segoe UI", 10, FontStyle.Bold) : _lblEtapeCourante.ForeColor = Color.DarkBlue : _lblEtapeCourante.Dock = DockStyle.Fill
        pnlB.Controls.Add(_lblEtapeCourante, 1, 1)
        _pbarEtape.Dock = DockStyle.Fill : _pbarEtape.Style = ProgressBarStyle.Continuous
        pnlB.Controls.Add(_pbarEtape, 0, 2)
        _lblSecu.ForeColor = Color.DarkOrange : _lblSecu.Font = New Font("Segoe UI", 9, FontStyle.Italic) : _lblSecu.Dock = DockStyle.Fill
        pnlB.Controls.Add(_lblSecu, 1, 2)
        _btnSauverChrono.Text = "💾 Sauvegarder" : _btnSauverChrono.BackColor = Color.FromArgb(60, 65, 80) : _btnSauverChrono.ForeColor = Color.White : _btnSauverChrono.FlatStyle = FlatStyle.Flat : _btnSauverChrono.Width = 140
        Dim pnlSav As New FlowLayoutPanel() With {.Dock = DockStyle.Fill}
        pnlSav.Controls.Add(_btnSauverChrono)
        pnlB.Controls.Add(pnlSav, 0, 3) : pnlB.SetColumnSpan(pnlSav, 2)

        split.Panel1.Controls.Add(pnlH)
        split.Panel2.Controls.Add(pnlB)

        ' ── Section règles conditionnelles ──
        ' Insérer un 3ème panneau dans Panel2 (au-dessus du panneau de contrôle)
        Dim splitB As New SplitContainer()
        splitB.Dock        = DockStyle.Fill
        splitB.Orientation = Orientation.Horizontal
        splitB.Panel1.Controls.Add(ConstruirePanneauRegles())
        splitB.Panel2.Controls.Add(pnlB)
        split.Panel2.Controls.Clear()
        split.Panel2.Controls.Add(splitB)

        _tabChrono.Controls.Add(split)
    End Sub

    ' ─── Règles conditionnelles ───────────────────────────────────────────────

    Private _dgvRegles As New DataGridView()
    Private _btnAjouterRegle   As New Button()
    Private _btnSupprimerRegle As New Button()

    Private Function ConstruirePanneauRegles() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        Dim lbl As New Label() With {
            .Text      = "RÈGLES CONDITIONNELLES  —  Si [voie] [condition] [valeur] → Déclencher [relais]",
            .Dock      = DockStyle.Top,
            .Height    = 22,
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(140, 80, 180),
            .Padding   = New Padding(6, 4, 0, 0)
        }

        ' Barre de boutons
        Dim tb As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .Height = 30, .Padding = New Padding(4, 2, 0, 0)}
        _btnAjouterRegle.Text   = "+ Ajouter règle"
        _btnAjouterRegle.Width  = 120
        _btnAjouterRegle.Height = 28
        _btnAjouterRegle.FlatStyle = FlatStyle.Flat
        _btnAjouterRegle.BackColor = Color.FromArgb(110, 60, 160)
        _btnAjouterRegle.ForeColor = Color.White

        _btnSupprimerRegle.Text   = "✕ Supprimer"
        _btnSupprimerRegle.Width  = 95
        _btnSupprimerRegle.Height = 28
        _btnSupprimerRegle.FlatStyle = FlatStyle.Flat
        _btnSupprimerRegle.BackColor = Color.FromArgb(160, 50, 40)
        _btnSupprimerRegle.ForeColor = Color.White

        Dim lblInfo As New Label() With {
            .Text      = "  Exemples : Voie 108 > 35 → Relais Pompe    |    Voie 126 < 12 → Relais Réchauffeur",
            .AutoSize  = True,
            .ForeColor = Color.Gray,
            .Font      = New Font("Segoe UI", 8, FontStyle.Italic),
            .Margin    = New Padding(10, 5, 0, 0)
        }
        tb.Controls.AddRange({_btnAjouterRegle, _btnSupprimerRegle, lblInfo})

        ' Grille des règles
        _dgvRegles.Dock                  = DockStyle.Fill
        _dgvRegles.AllowUserToAddRows    = False
        _dgvRegles.AllowUserToDeleteRows = False
        _dgvRegles.RowHeadersVisible     = False
        _dgvRegles.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvRegles.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgvRegles.Font                  = New Font("Segoe UI", 9)
        _dgvRegles.BackgroundColor       = Color.FromArgb(252, 248, 255)
        _dgvRegles.SelectionMode         = DataGridViewSelectionMode.FullRowSelect

        ' Colonne active
        Dim colActif As New DataGridViewCheckBoxColumn() With {
            .Name = "rActif", .HeaderText = "Active", .Width = 55
        }
        ' Colonne voie (saisie libre : ex "C1_V108" ou "108")
        Dim colVoie As New DataGridViewComboBoxColumn() With {
            .Name = "rVoie", .HeaderText = "Voie mesurée", .Width = 160
        }
        ' Colonne opérateur
        Dim colOp As New DataGridViewComboBoxColumn() With {
            .Name = "rOp", .HeaderText = "Condition", .Width = 90
        }
        colOp.Items.AddRange({">", ">=", "<", "<=", "="})

        ' Colonne valeur seuil
        Dim colVal As New DataGridViewTextBoxColumn() With {
            .Name = "rVal", .HeaderText = "Valeur seuil", .Width = 100
        }
        ' Colonne relais cible
        Dim colRelais As New DataGridViewComboBoxColumn() With {
            .Name = "rRelais", .HeaderText = "→ Déclencher relais", .Width = 200
        }
        ' Colonne action
        Dim colAction As New DataGridViewComboBoxColumn() With {
            .Name = "rAction", .HeaderText = "Action", .Width = 100
        }
        colAction.Items.AddRange({"Activer (ON)", "Désactiver (OFF)", "Régler tension (V)"})

        Dim colTensionCible As New DataGridViewTextBoxColumn() With {
            .Name        = "rTension",
            .HeaderText  = "Tension cible (V)",
            .Width       = 110,
            .ToolTipText = "Tension à appliquer quand l'action est 'Régler tension'." & vbCrLf &
                           "Mode Analogique : valeur positive (0 à +Amplitude)." & vbCrLf &
                           "Mode Analogique full : valeur positive ou négative (−Amplitude à +Amplitude)."
        }

        ' Colonne description auto
        Dim colDesc As New DataGridViewTextBoxColumn() With {
            .Name = "rDesc", .HeaderText = "Description", .ReadOnly = False
        }

        _dgvRegles.Columns.AddRange({colActif, colVoie, colOp, colVal, colRelais, colAction, colTensionCible, colDesc})

        ' Style en-têtes
        _dgvRegles.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 220, 245)
        _dgvRegles.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 40, 120)
        _dgvRegles.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgvRegles.EnableHeadersVisualStyles = False

        AddHandler _dgvRegles.CellValueChanged,  AddressOf Regles_CellValueChanged
        AddHandler _dgvRegles.DataError, Sub(s, ev) ev.ThrowException = False
        AddHandler _dgvEtapes.DataError, Sub(s, ev) ev.ThrowException = False
        AddHandler _btnAjouterRegle.Click,        AddressOf BtnAjouterRegle_Click
        AddHandler _btnSupprimerRegle.Click,      AddressOf BtnSupprimerRegle_Click

        pnl.Controls.Add(_dgvRegles)
        pnl.Controls.Add(tb)
        pnl.Controls.Add(lbl)
        Return pnl
    End Function

    ''' <summary>Met à jour les listes déroulantes Voie et Relais dans la grille des règles.</summary>
    Public Sub ActualiserListesRegles()
        Dim colVoie   = TryCast(_dgvRegles.Columns("rVoie"),   DataGridViewComboBoxColumn)
        Dim colRelais = TryCast(_dgvRegles.Columns("rRelais"), DataGridViewComboBoxColumn)
        If colVoie Is Nothing OrElse colRelais Is Nothing Then Return

        colVoie.Items.Clear()
        colRelais.Items.Clear()

        For Each c In _gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                colVoie.Items.Add(String.Format("[{0}] Voie {1} — {2}", c.NomAffiche, v.Numero, v.Nom))
            Next
            For Each s In c.Voies.SortiesActives()
                colRelais.Items.Add(String.Format("[{0}] {1} (S{2})", c.NomAffiche, s.Nom, s.Numero))
            Next
        Next
    End Sub

    Private Sub BtnAjouterRegle_Click(sender As Object, e As EventArgs)
        ActualiserListesRegles()
        _dgvRegles.Rows.Add(True, "", ">", "", "", "Activer (ON)", "", "")
    End Sub

    Private Sub BtnSupprimerRegle_Click(sender As Object, e As EventArgs)
        If _dgvRegles.SelectedRows.Count > 0 Then
            _dgvRegles.Rows.Remove(_dgvRegles.SelectedRows(0))
        End If
    End Sub

    Private Sub Regles_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        MettreAJourDescriptionRegle(e.RowIndex)
    End Sub

    Private Sub MettreAJourDescriptionRegle(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= _dgvRegles.Rows.Count Then Return
        Dim row    = _dgvRegles.Rows(rowIndex)
        Dim descActuelle = If(row.Cells("rDesc").Value IsNot Nothing, row.Cells("rDesc").Value.ToString(), "")
        ' Ne générer la description auto que si la cellule est vide
        If descActuelle <> "" Then Return
        Dim voie   = If(row.Cells("rVoie").Value   IsNot Nothing, row.Cells("rVoie").Value.ToString(),   "")
        Dim op     = If(row.Cells("rOp").Value     IsNot Nothing, row.Cells("rOp").Value.ToString(),     "")
        Dim val    = If(row.Cells("rVal").Value    IsNot Nothing, row.Cells("rVal").Value.ToString(),    "")
        Dim relais = If(row.Cells("rRelais").Value IsNot Nothing, row.Cells("rRelais").Value.ToString(), "")
        Dim action = If(row.Cells("rAction").Value IsNot Nothing, row.Cells("rAction").Value.ToString(), "")
        If voie <> "" AndAlso op <> "" AndAlso val <> "" AndAlso relais <> "" Then
            row.Cells("rDesc").Value = String.Format("Si {0} {1} {2} → {3} {4}", voie, op, val, relais, action)
        End If
    End Sub

    ''' <summary>Convertit la grille des règles en liste de RegleConditionnelle pour le moteur.</summary>
    Public Function ObtenirRegles() As List(Of RegleConditionnelle)
        Dim regles As New List(Of RegleConditionnelle)
        For Each row As DataGridViewRow In _dgvRegles.Rows
            Dim actif = CBool(If(row.Cells("rActif").Value, False))
            If Not actif Then Continue For

            Dim voieStr   = If(row.Cells("rVoie").Value   IsNot Nothing, row.Cells("rVoie").Value.ToString(),   "")
            Dim opStr     = If(row.Cells("rOp").Value     IsNot Nothing, row.Cells("rOp").Value.ToString(),     "")
            Dim valStr    = If(row.Cells("rVal").Value    IsNot Nothing, row.Cells("rVal").Value.ToString(),    "")
            Dim relaisStr = If(row.Cells("rRelais").Value IsNot Nothing, row.Cells("rRelais").Value.ToString(), "")
            Dim actionStr = If(row.Cells("rAction").Value IsNot Nothing, row.Cells("rAction").Value.ToString(), "")

            Dim seuil As Double
            If Not Double.TryParse(valStr.Replace(",", "."),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, seuil) Then Continue For

            ' Extraire le numéro de voie depuis la description "[C1] Voie 108 — ..."
            Dim numeroVoie As Integer = -1
            Dim matchVoie = System.Text.RegularExpressions.Regex.Match(voieStr, "Voie (\d+)")
            If matchVoie.Success Then Integer.TryParse(matchVoie.Groups(1).Value, numeroVoie)
            Dim numeroCentrale As Integer = 1
            Dim matchC = System.Text.RegularExpressions.Regex.Match(voieStr, "\[.*?(\d+)\]")
            If matchC.Success Then Integer.TryParse(matchC.Groups(1).Value, numeroCentrale)

            ' Extraire numéro sortie depuis "[C1] Nom (S123)"
            Dim numeroSortie As Integer = -1
            Dim matchS = System.Text.RegularExpressions.Regex.Match(relaisStr, "\(S(\d+)\)")
            If matchS.Success Then Integer.TryParse(matchS.Groups(1).Value, numeroSortie)
            Dim centraleSortie As Integer = 1
            Dim matchCS = System.Text.RegularExpressions.Regex.Match(relaisStr, "\[.*?(\d+)\]")
            If matchCS.Success Then Integer.TryParse(matchCS.Groups(1).Value, centraleSortie)

            If numeroVoie < 0 OrElse numeroSortie < 0 Then Continue For

            Dim tensionStr = If(row.Cells("rTension").Value IsNot Nothing, row.Cells("rTension").Value.ToString(), "")
            Dim tensionCible As Double = 0.0
            Double.TryParse(tensionStr.Replace(",", "."),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, tensionCible)

            Dim typeAction = If(actionStr.Contains("Régler tension"),
                RegleConditionnelle.TypeActionRegle.ReglerTension,
                If(actionStr.Contains("OFF"),
                   RegleConditionnelle.TypeActionRegle.Desactiver,
                   RegleConditionnelle.TypeActionRegle.Activer))

            regles.Add(New RegleConditionnelle() With {
                .NumeroCentraleVoie   = numeroCentrale,
                .NumeroVoie           = numeroVoie,
                .Operateur            = opStr,
                .ValeurSeuil          = seuil,
                .NumeroCentraleSortie = centraleSortie,
                .NumeroSortie         = numeroSortie,
                .TypeAction           = typeAction,
                .TensionCible         = tensionCible
            })
        Next
        Return regles
    End Function

    ''' <summary>
    ''' Recrée les colonnes de la grille chronogramme selon les relais actifs.
    ''' Une colonne checkbox par relais dynamique.
    ''' </summary>
    ''' <summary>
    ''' Pour chaque sortie active :
    '''   - Mode Booléen   → 1 colonne CheckBox  (id = "C1_S123")
    '''   - Mode Analogique → 2 colonnes :
    '''       * 1 colonne texte tension globale par défaut (id = "C1_S123_DEF")
    '''       * 1 colonne texte tension par étape avec surcharge (id = "C1_S123")
    ''' </summary>
    Private Sub ReconstruireColonnesChronogramme()
        For Each nomCol In _colonnesChronoRelais
            If _dgvEtapes.Columns.Contains(nomCol) Then _dgvEtapes.Columns.Remove(nomCol)
        Next
        ' Supprimer aussi les colonnes _DEF (ancien format)
        Dim toRemove = _dgvEtapes.Columns.Cast(Of DataGridViewColumn)() _
            .Where(Function(c) c.Name.EndsWith("_DEF") OrElse c.Name.EndsWith("_MNT")) _
            .Select(Function(c) c.Name).ToList()
        For Each nom In toRemove
            _dgvEtapes.Columns.Remove(nom)
        Next
        _colonnesChronoRelais.Clear()

        For Each item In _gestionnaire.ToutesSortiesActives()
            Dim id      = HistoriqueMultiCentrale.CleSortie(item.Centrale.Numero, item.Sortie.Numero)
            Dim nomBase = String.Format("[{0}] {1}", item.Centrale.NomAffiche, item.Sortie.Nom)
            Dim amp     = item.Sortie.Amplitude

            Select Case item.Sortie.Mode
                Case SortieAnalogique.ModePilotage.Analogique
                    Dim colEtape As New DataGridViewTextBoxColumn() With {
                        .Name        = id,
                        .HeaderText  = nomBase & " (V)",
                        .Width       = 120,
                        .ToolTipText = String.Format(
                            "Tension 0 à +{0:F1} V.{1}Vide = 0 V (sauf si case Maintien cochée).", amp, vbCrLf)
                    }
                    _dgvEtapes.Columns.Add(colEtape)
                    ' Case à cocher Maintien
                    Dim colMnt As New DataGridViewCheckBoxColumn() With {
                        .Name        = id & "_MNT",
                        .HeaderText  = nomBase & " maintien",
                        .Width       = 90,
                        .ToolTipText = "Coché : conserver la tension de l'étape précédente si la cellule tension est vide."
                    }
                    _dgvEtapes.Columns.Add(colMnt)

                Case SortieAnalogique.ModePilotage.AnalogiqueFull
                    Dim colAnaFull As New DataGridViewTextBoxColumn() With {
                        .Name        = id,
                        .HeaderText  = nomBase & " (V)",
                        .Width       = 130,
                        .ToolTipText = String.Format(
                            "Tension de sortie : de −{0:F1} V à +{0:F1} V.{1}" &
                            "Valeur négative : tension dans le sens −.{1}" &
                            "Valeur positive : tension dans le sens +.{1}" &
                            "0 V = neutre/arrêt.{1}Vide = 0 V sauf si case Maintien cochée.", amp, vbCrLf)
                    }
                    _dgvEtapes.Columns.Add(colAnaFull)
                    Dim colMntAnaFull As New DataGridViewCheckBoxColumn() With {
                        .Name        = id & "_MNT",
                        .HeaderText  = nomBase & " maintien",
                        .Width       = 90,
                        .ToolTipText = "Coché : conserver la tension de l'étape précédente si la cellule est vide."
                    }
                    _dgvEtapes.Columns.Add(colMntAnaFull)

                Case Else   ' Booléen
                    Dim col As New DataGridViewCheckBoxColumn() With {
                        .Name        = id,
                        .HeaderText  = nomBase,
                        .Width       = 120
                    }
                    _dgvEtapes.Columns.Add(col)
            End Select

            _colonnesChronoRelais.Add(id)
        Next
    End Sub

    Private Sub BtnAjouterEtape_Click(s As Object, e As EventArgs)
        Dim vals As New List(Of Object) From {"Étape " & (_dgvEtapes.Rows.Count + 1), 60}
        For Each item In _gestionnaire.ToutesSortiesActives()
            Select Case item.Sortie.Mode
                Case SortieAnalogique.ModePilotage.Analogique,
                     SortieAnalogique.ModePilotage.AnalogiqueFull
                    vals.Add("")     ' tension : vide
                    vals.Add(False)  ' maintien : non coché
                Case Else
                    vals.Add(False)  ' booléen
            End Select
        Next
        Dim idx = _dgvEtapes.Rows.Add(vals.ToArray())
        _dgvEtapes.CurrentCell = _dgvEtapes.Rows(idx).Cells(0)
        _dgvEtapes.BeginEdit(True)
    End Sub

    Private Sub BtnSupprimerEtape_Click(s As Object, e As EventArgs)
        If _dgvEtapes.SelectedRows.Count > 0 Then
            _dgvEtapes.Rows.Remove(_dgvEtapes.SelectedRows(0))
        End If
    End Sub

    Private Sub EchangerEtape(dir As Integer)
        If _dgvEtapes.SelectedRows.Count = 0 Then Return
        Dim idx = _dgvEtapes.SelectedRows(0).Index
        Dim cib = idx + dir
        If cib < 0 OrElse cib >= _dgvEtapes.Rows.Count Then Return
        Dim va = (From c As DataGridViewCell In _dgvEtapes.Rows(idx).Cells Select c.Value).ToArray()
        Dim vb = (From c As DataGridViewCell In _dgvEtapes.Rows(cib).Cells Select c.Value).ToArray()
        For i = 0 To va.Length - 1
            _dgvEtapes.Rows(idx).Cells(i).Value = vb(i)
            _dgvEtapes.Rows(cib).Cells(i).Value = va(i)
        Next
        _dgvEtapes.Rows(cib).Selected = True
    End Sub

    Private Sub BtnDemarrerChrono_Click(s As Object, e As EventArgs)
        _chronogramme.Etapes.Clear()
        For Each row As DataGridViewRow In _dgvEtapes.Rows
            Dim etape As New EtapeChronogramme() With {
                .Nom           = If(row.Cells("colNom").Value IsNot Nothing, row.Cells("colNom").Value.ToString(), ""),
                .DureeSecondes = CInt(Math.Max(1,
                    If(ParseurDuree.EstValide(If(row.Cells("colDuree").Value IsNot Nothing,
                        row.Cells("colDuree").Value.ToString(), "60")),
                        ParseurDuree.EnSecondes(If(row.Cells("colDuree").Value IsNot Nothing,
                            row.Cells("colDuree").Value.ToString(), "60")), 60)))
            }
            For Each item In _gestionnaire.ToutesSortiesActives()
                Dim id    = HistoriqueMultiCentrale.CleSortie(item.Centrale.Numero, item.Sortie.Numero)
                Dim idMnt = id & "_MNT"

                Select Case item.Sortie.Mode
                    Case SortieAnalogique.ModePilotage.Analogique,
                         SortieAnalogique.ModePilotage.AnalogiqueFull
                        ' Lire tension de l'étape
                        Dim etapeStr = If(_dgvEtapes.Columns.Contains(id) AndAlso
                                          row.Cells(id).Value IsNot Nothing,
                                          row.Cells(id).Value.ToString(), "")
                        Dim etapeVal As Double = Double.NaN
                        If etapeStr <> "" Then
                            Double.TryParse(etapeStr.Replace(",", "."),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, etapeVal)
                        End If
                        etape.TensionsSorties(id) = etapeVal

                        ' Lire la case Maintien et mettre à jour le RelaisDynamique
                        Dim maintien = _dgvEtapes.Columns.Contains(idMnt) AndAlso
                                       CBool(If(row.Cells(idMnt).Value, False))
                        Dim rd = _chronogramme.RelaisDynamiques.FirstOrDefault(Function(r) r.Id = id)
                        If rd IsNot Nothing Then rd.Maintien = maintien

                    Case Else   ' Booléen
                        If row.Cells(id) IsNot Nothing Then
                            etape.EtatsRelais(id) = CBool(If(row.Cells(id).Value, False))
                        End If
                End Select
            Next
            _chronogramme.Etapes.Add(etape)
        Next
        If _chronogramme.Etapes.Count = 0 Then
            MessageBox.Show("Ajoutez au moins une étape.", "Chronogramme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Dim f As Integer
        Select Case _cmbUniteDuree.SelectedIndex
            Case 0 : f = 1           ' secondes
            Case 1 : f = 60          ' minutes
            Case 2 : f = 3600        ' heures
            Case 3 : f = 86400       ' jours
            Case Else : f = 3600
        End Select
        _chronogramme.DureeTotaleSecondes = CInt(_numDureeCycle.Value) * f
        _chronogramme.BouclerSurDuree     = _chkBoucler.Checked
        Dim regles = ObtenirRegles()
        _chronogramme.Regles              = regles

        ' ── Construire le contexte chronogramme pour le CSV ──
        Dim ctx As New ContexteCSVChronogramme() With {
            .DureeTotale   = _numDureeCycle.Value.ToString() & " " &
                             If(_cmbUniteDuree.SelectedItem IsNot Nothing, _cmbUniteDuree.SelectedItem.ToString(), ""),
            .Boucler       = _chkBoucler.Checked,
            .ArreterAcqFin = _chkArreterAcqFinChrono.Checked
        }
        ' Résumé des étapes
        For Each row As DataGridViewRow In _dgvEtapes.Rows
            Dim nom   = If(row.Cells("colNom").Value   IsNot Nothing, row.Cells("colNom").Value.ToString(),   "?")
            Dim duree = If(row.Cells("colDuree").Value IsNot Nothing, row.Cells("colDuree").Value.ToString(), "?")
            ctx.Etapes.Add(String.Format("{0} ; {1} s", nom, duree))
        Next
        ' Résumé des règles
        For Each regle In regles
            Dim sortieNom = ""
            Dim c2 = _gestionnaire.ObtenirCentrale(regle.NumeroCentraleSortie)
            If c2 IsNot Nothing Then
                Dim s2 = c2.Voies.TrouverSortie(regle.NumeroSortie)
                If s2 IsNot Nothing Then sortieNom = s2.Nom
            End If
            Dim voieNom = ""
            Dim c1 = _gestionnaire.ObtenirCentrale(regle.NumeroCentraleVoie)
            If c1 IsNot Nothing Then
                Dim v1 = c1.Voies.TrouverVoie(regle.NumeroVoie)
                If v1 IsNot Nothing Then voieNom = v1.Nom
            End If
            Select Case regle.TypeAction
                Case RegleConditionnelle.TypeActionRegle.ReglerTension
                    ctx.Regles.Add(String.Format("Si {0} {1} {2:F2} → {3} = {4:F2} V",
                        voieNom, regle.Operateur, regle.ValeurSeuil, sortieNom, regle.TensionCible))
                Case RegleConditionnelle.TypeActionRegle.Activer
                    ctx.Regles.Add(String.Format("Si {0} {1} {2:F2} → {3} ON",
                        voieNom, regle.Operateur, regle.ValeurSeuil, sortieNom))
                Case RegleConditionnelle.TypeActionRegle.Desactiver
                    ctx.Regles.Add(String.Format("Si {0} {1} {2:F2} → {3} OFF",
                        voieNom, regle.Operateur, regle.ValeurSeuil, sortieNom))
            End Select
        Next
        _acquisition.ContexteChronogramme = ctx
        _chronogramme.Demarrer()

        ' ── Démarrer ou reconfigurer l'acquisition ──
        If Not _acquisition.EnCours Then
            ' Acquisition pas encore lancée : la démarrer avec CSV forcé
            _acquisition.IntervalleMsec = CInt(ParseurDuree.EnSecondes(
                CInt(_numIntervalle.Value).ToString() &
                ParseurDuree.SuffixeParIndex(_cmbUniteIntervalle.SelectedIndex)) * 1000)
            _acquisition.StockerCSV     = True   ' CSV forcé quand le chronogramme pilote
            _acquisition.ModeSim        = If(_chkSimulation.Checked,
                MoteurAcquisition.ModeSimulation.MonteeEnTemperature,
                MoteurAcquisition.ModeSimulation.Desactive)
            _acquisition.CheminCSV = _ongletCSV.CheminFige()
            _gestionnaire.FormatCSV          = _ongletCSV.FormatValeur
            _gestionnaire.LibelleUniteDuree  = _ongletCSV.LibelleUniteDuree
            _gestionnaire.DiviseurDuree      = _ongletCSV.DiviseurDuree
            _gestionnaire.HeureDepart        = DateTime.Now
            _gestCalculs.ResetIntegrations()
            _acquisition.GestCalculs = _gestCalculs
            _acquisition.Historique  = _historique
            If _acquisition.Demarrer() Then
                _acqDemarreeParChrono   = True
                _btnDemarrerAcq.Enabled = False
                _btnArreterAcq.Enabled  = True
                ' Génération automatique du rapport avec contexte chronogramme
                If _ongletRapport.AutoGenerer Then
                    _generateurRapport.ChronoActif   = True
                    _generateurRapport.DgvEtapes     = _dgvEtapes
                    _generateurRapport.DgvRegles     = _dgvRegles
                    _generateurRapport.ArreterAcqFin = _chkArreterAcqFinChrono.Checked
                    _generateurRapport.Operateur   = _ongletRapport.Operateur
                    _generateurRapport.Laboratoire = _ongletRapport.Laboratoire
                    _generateurRapport.Projet      = _ongletRapport.Projet
                    _generateurRapport.Notes       = _ongletRapport.Notes
                    _generateurRapport.CheminLogo  = _ongletRapport.CheminLogo
                    _generateurRapport.NomPolice   = _ongletRapport.NomPolice
                    Task.Run(Sub() _ongletRapport.GenererRapport("Chrono"))
                End If
                AfficherStatut("Chronogramme démarré  +  Acquisition démarrée (CSV activé automatiquement)")
            Else
                _acqDemarreeParChrono = False
                AfficherStatut("Chronogramme démarré  (acquisition non disponible)", True)
            End If
        Else
            ' Acquisition déjà en cours : forcer l'écriture CSV si elle était désactivée
            If Not _acquisition.StockerCSV Then
                _acquisition.StockerCSV = True
                _acquisition.CheminCSV  = _ongletCSV.CheminFige()
                AfficherStatut("Chronogramme démarré  (CSV activé sur acquisition existante)")
            Else
                AfficherStatut("Chronogramme démarré  (acquisition déjà en cours)")
            End If
            _acqDemarreeParChrono = False
        End If

        ' ── Prise de main sur les relais : désactiver le mode manuel ──
        _chkModeManuel.Checked = False
        ActualiserDispoRelais()   ' désactive les boutons manuels

        _btnDemarrerChrono.Enabled = False
        _btnArreterChrono.Enabled  = True
    End Sub

    Private Sub BtnArreterChrono_Click(s As Object, e As EventArgs)
        _chronogramme.Arreter()
        _btnDemarrerChrono.Enabled = True
        _btnArreterChrono.Enabled  = False

        ' Rendre la main sur les relais (mode manuel possible à nouveau)
        ' Note : on ne force pas _chkModeManuel.Checked = True,
        '        l'utilisateur choisit s'il veut reprendre le contrôle manuellement.
        ' On réactive simplement les boutons si mode manuel était actif.
        ActualiserDispoRelais()

        ' Arrêter l'acquisition seulement si c'est le chronogramme qui l'avait lancée
        If _acqDemarreeParChrono AndAlso _acquisition.EnCours Then
            _acquisition.Arreter()
            _acqDemarreeParChrono   = False
            _btnDemarrerAcq.Enabled = True
            _btnArreterAcq.Enabled  = False
            AfficherStatut("Chronogramme et acquisition arrêtés")
        Else
            AfficherStatut("Chronogramme arrêté")
        End If
    End Sub

    Private Sub BtnSauverChrono_Click(s As Object, e As EventArgs)
        _config.Set_(ConfigManager.SEC_CHRONO, "DureeCycle", CInt(_numDureeCycle.Value))
        _config.Set_(ConfigManager.SEC_CHRONO, "UniteDuree", _cmbUniteDuree.SelectedIndex)
        _config.Set_(ConfigManager.SEC_CHRONO, "Boucler",    _chkBoucler.Checked)
        _config.Set_(ConfigManager.SEC_CHRONO, "ArreterAcqFinChrono", _chkArreterAcqFinChrono.Checked)
        Dim n = _dgvEtapes.Rows.Count
        _config.Set_(ConfigManager.SEC_CHRONO, "NbEtapes", n)
        For i As Integer = 0 To n - 1
            Dim r = _dgvEtapes.Rows(i)
            _config.Set_(ConfigManager.SEC_CHRONO, "Etape" & i & "_Nom",
                If(r.Cells("colNom").Value IsNot Nothing, r.Cells("colNom").Value.ToString(), ""))
            _config.Set_(ConfigManager.SEC_CHRONO, "Etape" & i & "_Duree",
                CInt(If(r.Cells("colDuree").Value, 60)))
            For Each item In _gestionnaire.ToutesSortiesActives()
                Dim id    = HistoriqueMultiCentrale.CleSortie(item.Centrale.Numero, item.Sortie.Numero)
                Dim idMnt = id & "_MNT"
                Select Case item.Sortie.Mode
                    Case SortieAnalogique.ModePilotage.Analogique,
                         SortieAnalogique.ModePilotage.AnalogiqueFull
                        ' Tension
                        Dim tensStr = If(_dgvEtapes.Columns.Contains(id) AndAlso
                                         r.Cells(id).Value IsNot Nothing,
                                         r.Cells(id).Value.ToString(), "")
                        _config.Set_(ConfigManager.SEC_CHRONO, "Etape" & i & "_" & id, tensStr)
                        ' Maintien
                        Dim mnt = _dgvEtapes.Columns.Contains(idMnt) AndAlso
                                  CBool(If(r.Cells(idMnt).Value, False))
                        _config.Set_(ConfigManager.SEC_CHRONO, "Etape" & i & "_" & idMnt, mnt)
                    Case Else
                        If _dgvEtapes.Columns.Contains(id) AndAlso r.Cells(id) IsNot Nothing Then
                            _config.Set_(ConfigManager.SEC_CHRONO, "Etape" & i & "_" & id,
                                CBool(If(r.Cells(id).Value, False)))
                        End If
                End Select
            Next
        Next
        SauvegarderConfig()

        ' Sauvegarder aussi les règles conditionnelles
        Dim nR = _dgvRegles.Rows.Count
        _config.Set_(ConfigManager.SEC_CHRONO, "NbRegles", nR)
        For i As Integer = 0 To nR - 1
            Dim r = _dgvRegles.Rows(i)
            Dim pref = "Regle" & i & "_"
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Actif",  CBool(If(r.Cells("rActif").Value, False)))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Voie",   CellStr2(r, "rVoie"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Op",     CellStr2(r, "rOp"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Val",    CellStr2(r, "rVal"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Relais", CellStr2(r, "rRelais"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Action",  CellStr2(r, "rAction"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Tension", CellStr2(r, "rTension"))
            _config.Set_(ConfigManager.SEC_CHRONO, pref & "Desc",    CellStr2(r, "rDesc"))
        Next
        ' Inclure les styles graphique dans chaque sauvegarde
        _panelGraphique.Styles.SauverDansConfig(_config)
        _config.Sauvegarder()
        AfficherStatut("Chronogramme sauvegardé.")
    End Sub

    Private Function CellStr2(row As DataGridViewRow, col As String) As String
        Return If(row.Cells(col).Value IsNot Nothing, row.Cells(col).Value.ToString(), "")
    End Function

    Private Sub ChargerConfigChrono()
        ' Paramètres simples uniquement — les règles sont chargées dans OnLoad
        ' après ActualiserListesRegles (les ComboBox rVoie/rRelais doivent être remplis)
        _numDureeCycle.Value         = _config.GetInt(ConfigManager.SEC_CHRONO, "DureeCycle", 24)
        _cmbUniteDuree.SelectedIndex = _config.GetInt(ConfigManager.SEC_CHRONO, "UniteDuree", 2)
        _chkBoucler.Checked          = _config.GetBool(ConfigManager.SEC_CHRONO, "Boucler", True)
        _chkArreterAcqFinChrono.Checked = _config.GetBool(ConfigManager.SEC_CHRONO, "ArreterAcqFinChrono", False)
    End Sub

    ''' <summary>
    ''' Recharge les règles conditionnelles depuis config.ini.
    ''' Doit être appelé APRÈS ActualiserListesRegles (ComboBox remplis).
    ''' </summary>
    Private Sub ChargerReglesDepuisConfig()
        Dim nb = _config.GetInt(ConfigManager.SEC_CHRONO, "NbRegles", 0)
        _dgvRegles.Rows.Clear()
        For i As Integer = 0 To nb - 1
            Dim pref   = "Regle" & i & "_"
            Dim actif  = _config.GetBool(ConfigManager.SEC_CHRONO, pref & "Actif",  True)
            Dim voie   = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Voie",   "")
            Dim op     = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Op",     ">")
            Dim val    = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Val",    "")
            Dim relais = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Relais", "")
            Dim action  = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Action",  "Activer (ON)")
            Dim tension = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Tension", "")
            Dim desc    = _config.Get_(ConfigManager.SEC_CHRONO,    pref & "Desc",    "")
            ' Valider que l'action existe dans le ComboBox
            Dim colAction = TryCast(_dgvRegles.Columns("rAction"), DataGridViewComboBoxColumn)
            If colAction IsNot Nothing AndAlso Not colAction.Items.Contains(action) Then
                action = "Activer (ON)"
            End If
            _dgvRegles.Rows.Add(actif, voie, op, val, relais, action, tension, desc)
        Next
    End Sub

    ' ─── Utilitaires ──────────────────────────────────────────────────────────

    ' Chemin du fichier mémorisant le dernier config utilisé
    Private Shared ReadOnly CheminDernierConfig As String =
        IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Thermopilot", "dernier_config.txt")

    Private Shared Function LireDernierFichierConfig() As String
        Try
            If IO.File.Exists(CheminDernierConfig) Then
                Return IO.File.ReadAllText(CheminDernierConfig,
                    System.Text.Encoding.UTF8).Trim()
            End If
        Catch
        End Try
        Return ""
    End Function

    Private Shared Sub SauverDernierFichierConfig(chemin As String)
        Try
            IO.Directory.CreateDirectory(
                IO.Path.GetDirectoryName(CheminDernierConfig))
            IO.File.WriteAllText(CheminDernierConfig, chemin,
                System.Text.Encoding.UTF8)
        Catch
        End Try
    End Sub

    Private ReadOnly Property TITRE_BASE As String
        Get
            Return AppInfo.TitreComplet
        End Get
    End Property

    Private Sub MettreAJourTitre()
        Dim nomFichier = IO.Path.GetFileName(ConfigManager.CheminFichier)
        Me.Text = TITRE_BASE & "  —  " & nomFichier
    End Sub

    Private Sub MettreAJourFenetre()
        Dim fen As Integer = 0
        If _numFenetre.Value > 0 Then
            Try
                fen = CInt(ParseurDuree.EnSecondes(
                    CInt(_numFenetre.Value).ToString() &
                    ParseurDuree.SuffixeParIndex(_cmbUniteFenetre.SelectedIndex)))
            Catch
            End Try
        End If
        _panelGraphique.FenetreSecondes = fen
    End Sub

    Private Sub SauvegarderConfig()
        Try
            ' Sauvegarder connexion (IP, ports, nb centrales)
            _ongletConnexion.SauverVersConfig()
            ' Sauvegarder les styles graphique avant le flush
            _panelGraphique.Styles.SauverDansConfig(_config)
            ' Sauvegarder les sous-onglets système
            _ongletSysteme.SauverDansConfig()
            _gestCalculs.SauverDansConfig(_config)
            _config.Set_(ConfigManager.SEC_CHRONO, "ArreterAcqFinChrono", _chkArreterAcqFinChrono.Checked)
            _ongletRapport.SauverDansConfig()
            _config.Sauvegarder()
            AfficherStatut("Configuration sauvegardée → " &
                           Path.GetFileName(ConfigManager.CheminFichier))
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Erreur sauvegarde", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ─── Menu Fichier ────────────────────────────────────────────────────────

    Private Sub Menu_Nouveau(sender As Object, e As EventArgs)
        If MessageBox.Show(
            "Créer une nouvelle configuration vierge ?" & vbCrLf & vbCrLf &
            "⚠ Vous devrez redémarrer l'application pour que la" & vbCrLf &
            "ré-initialisation complète soit prise en compte.",
            "Nouveau", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        ConfigManager.CheminFichier =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "Thermopilot", "config.ini")
        _config = New ConfigManager()
        _config.AppliquerDefauts()
        MettreAJourTitre()
        AfficherStatut("Nouvelle configuration créée.")
    End Sub

    Private Sub Menu_Ouvrir(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog() With {
            .Title  = "Ouvrir une configuration Thermopilot",
            .Filter = "Config Thermopilot|*.ini|Tous|*.*",
            .InitialDirectory = Path.GetDirectoryName(ConfigManager.CheminFichier)}
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Try
                _config.ChargerDepuis(dlg.FileName)
                _ongletConnexion.ChargerDepuisConfig()
                _ongletCSV.ChargerDepuisConfig()
                ChargerConfigChrono()
                _gestCalculs.ChargerDepuisConfig(_config)
                _ongletCalculs.RemplirGrille()
                _ongletRapport.ChargerDepuisConfig()
                ActualiserListesRegles()
                ChargerReglesDepuisConfig()
                ChargerEtapesDepuisConfig()
                MettreAJourTitre()
                AfficherStatut("Config chargée : " & Path.GetFileName(dlg.FileName))
            Catch ex As Exception
                MessageBox.Show("Erreur chargement : " & ex.Message, "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub Menu_Sauver(sender As Object, e As EventArgs)
        SauvegarderConfig()
    End Sub

    Private Sub Menu_SauverSous(sender As Object, e As EventArgs)
        Using dlg As New SaveFileDialog() With {
            .Title      = "Enregistrer la configuration sous...",
            .Filter     = "Config Thermopilot|*.ini|Tous|*.*",
            .DefaultExt = "ini",
            .InitialDirectory = Path.GetDirectoryName(ConfigManager.CheminFichier),
            .FileName   = Path.GetFileName(ConfigManager.CheminFichier)}
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Try
                _panelGraphique.Styles.SauverDansConfig(_config)
                _ongletSysteme.SauverDansConfig()
                _gestCalculs.SauverDansConfig(_config)
                _config.Set_(ConfigManager.SEC_CHRONO, "ArreterAcqFinChrono", _chkArreterAcqFinChrono.Checked)
                _ongletRapport.SauverDansConfig()
                _config.SauvegarderVers(dlg.FileName)
                MettreAJourTitre()
                AfficherStatut("Config sauvegardée : " & Path.GetFileName(dlg.FileName))
            Catch ex As Exception
                MessageBox.Show("Erreur sauvegarde : " & ex.Message, "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    ' ─── Notifications utilisateur ───────────────────────────────────────────

    ''' <summary>
    ''' Crée un bouton "📌 Notification" standardisé pour toutes les barres d'onglet.
    ''' </summary>
    Private Function ConstruireBoutonNotification() As Button
        Dim btn As New Button() With {
            .Text      = "📌 Notification",
            .BackColor = Color.FromArgb(200, 110, 0),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Width     = 145,
            .Height    = 28,
            .Margin    = New Padding(8, 0, 0, 0)
        }
        AddHandler btn.Click, AddressOf BtnNotification_Click
        Return btn
    End Function

    Private Sub BtnNotification_Click(sender As Object, e As EventArgs)
        Using frm As New FormNotification()
            If frm.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim texte = frm.TexteNotification
            If String.IsNullOrWhiteSpace(texte) Then Return
            EnregistrerNotification(frm.Horodatage, texte)
            AfficherStatut("Notification enregistrée : " & texte.Replace(vbCrLf, " "))
        End Using
    End Sub

    ''' <summary>
    ''' Enregistre la notification comme ligne spéciale dans le CSV principal des mesures.
    ''' La ligne contient l'horodatage, des cellules vides pour toutes les mesures,
    ''' et le texte de notification dans la dernière colonne.
    ''' </summary>
    Private Sub EnregistrerNotification(horodatage As DateTime, texte As String)
        If Not _acquisition.StockerCSV OrElse Not _acquisition.EnCours Then
            MessageBox.Show(
                "L'acquisition CSV n'est pas active." & vbCrLf &
                "Démarrez l'acquisition avec l'option CSV cochée pour enregistrer des notifications.",
                "Notification non enregistrée", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        ' Mettre en file d'attente — sera écrite à la prochaine ligne de mesure
        _acquisition.MettreNotificationEnAttente(texte)
        AfficherStatut("📌 Notification en attente — sera enregistrée à la prochaine mesure.")
    End Sub

    Private Sub AjouterPanneauxInfoDebit()
        ' Acquisition
        _tabAcquisition.Controls.Add(_infoDebitAcq.ConstruirePanel(_gestionnaire))
        ' Relais
        _tabRelais.Controls.Add(_infoDebitRelais.ConstruirePanel(_gestionnaire))
        ' Chronogramme
        _tabChrono.Controls.Add(_infoDebitChrono.ConstruirePanel(_gestionnaire))
        ' Système
        _tabSysteme.Controls.Add(_infoDebitSys.ConstruirePanel(_gestionnaire))
    End Sub

    ''' <summary>Reconstruit le contenu des panneaux débit (après OnVoiesAppliquees).</summary>
    Private Sub ActualiserPanneauxInfoDebit()
        _infoDebitAcq.ActualiserContenu()
        _infoDebitRelais.ActualiserContenu()
        _infoDebitChrono.ActualiserContenu()
        _infoDebitSys.ActualiserContenu()
    End Sub

    Private Sub AfficherStatut(message As String, Optional erreur As Boolean = False)
        If InvokeRequired Then
            BeginInvoke(Sub() AfficherStatut(message, erreur))
            Return
        End If
        _lblStatut.Text      = message
        _lblStatut.ForeColor = If(erreur, Color.DarkRed, SystemColors.ControlText)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        ' 1. Charger la bibliothèque de périphériques (avant les onglets Centrale)
        _ongletPeripheriques.ChargerDepuisConfig()

        ' 2. Charger la config Système
        _ongletSysteme.ChargerDepuisConfig()

        ' 3. Initialiser les onglets Voies avec le nombre de centrales chargé
        Dim nb = _config.GetInt(ConfigManager.SEC_CONNEXION, "NbCentrales", 1)
        _gestionnaire.DefinirNombreCentrales(nb, _config)
        OnNbCentralesChange(Me, nb)

        ' 4. Ajouter les panneaux info débit repliables dans les 4 onglets
        '    (après que _gestionnaire est initialisé)
        AjouterPanneauxInfoDebit()

        ' 5. Propager les voies depuis la config vers le Gestionnaire (sans SCPI)
        '    OBLIGATOIREMENT avant ActualiserListeDepuisGestionnaire
        _gestionnaireVoies.PropagerTousVersGestionnaire()

        ' 5b. Actualiser la liste Système (les voies sont maintenant disponibles)
        _ongletSysteme.GestCalculs = _gestCalculs
        _ongletSysteme.ActualiserListeDepuisGestionnaire()
        _ongletCalculs.ActualiserListeVoies()

        ' 6. Charger les règles conditionnelles (après ActualiserListesRegles)
        ActualiserListesRegles()
        ChargerReglesDepuisConfig()

        ' 7. Recharger les étapes du chronogramme (colonnes maintenant disponibles)
        ChargerEtapesDepuisConfig()

        ' 8. Rafraîchir l'aperçu CSV maintenant que Gestionnaire et GestCalculs sont disponibles
        _ongletCSV.MettreAJourApercu()

        ' 9. Afficher le nom du fichier de config dans le titre
        MettreAJourTitre()

        ' 10. Recharger l'état des onglets Résultats (fichiers CSV + masques)
        _ongletVisuCSV.Config        = _config
        _ongletVisuCSV.DossierDefaut = _ongletCSV.DossierCSV
        _ongletVisuCSV.ChargerEtat()
    End Sub

    ''' <summary>Recharge les étapes du chronogramme depuis config.ini (appelé après OnLoad).</summary>
    Private Sub ChargerEtapesDepuisConfig()
        Dim nb = _config.GetInt(ConfigManager.SEC_CHRONO, "NbEtapes", 0)
        If nb = 0 Then Return
        _dgvEtapes.Rows.Clear()
        For i As Integer = 0 To nb - 1
            Dim nom   = _config.Get_(ConfigManager.SEC_CHRONO, "Etape" & i & "_Nom",   "Étape " & (i + 1))
            Dim duree = _config.GetInt(ConfigManager.SEC_CHRONO, "Etape" & i & "_Duree", 60)
            Dim vals As New List(Of Object) From {nom, duree}
            For Each item In _gestionnaire.ToutesSortiesActives()
                Dim id    = HistoriqueMultiCentrale.CleSortie(item.Centrale.Numero, item.Sortie.Numero)
                Dim idMnt = id & "_MNT"
                Dim cle   = "Etape" & i & "_" & id
                Dim cleMnt = "Etape" & i & "_" & idMnt
                Select Case item.Sortie.Mode
                    Case SortieAnalogique.ModePilotage.Analogique,
                         SortieAnalogique.ModePilotage.AnalogiqueFull
                        vals.Add(_config.Get_(ConfigManager.SEC_CHRONO, cle, ""))
                        vals.Add(_config.GetBool(ConfigManager.SEC_CHRONO, cleMnt, False))
                    Case Else
                        vals.Add(_config.GetBool(ConfigManager.SEC_CHRONO, cle, False))
                End Select
            Next
            _dgvEtapes.Rows.Add(vals.ToArray())
        Next
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _acquisition.Arreter()
        _chronogramme.Arreter()
        _gestionnaire.DeconnecterToutes()
        ' Sauvegarde automatique des styles et de la config à la fermeture
        Try
            _ongletConnexion.SauverVersConfig()
            _ongletCalculs.AppliquerEditionCourante()  ' forcer synchro UI→objet
            _panelGraphique.Styles.SauverDansConfig(_config)
            _ongletSysteme.SauverDansConfig()
            _gestCalculs.SauverDansConfig(_config)
            _config.Set_(ConfigManager.SEC_CHRONO, "ArreterAcqFinChrono", _chkArreterAcqFinChrono.Checked)
            _ongletRapport.SauverDansConfig()
            _ongletVisuCSV.SauverEtat()
            _config.Sauvegarder()
            ' Mémoriser le dernier fichier de config utilisé
            SauverDernierFichierConfig(ConfigManager.CheminFichier)
        Catch
        End Try
        MyBase.OnFormClosing(e)
    End Sub

End Class
