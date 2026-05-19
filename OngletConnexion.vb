Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Onglet Connexion — gestion multi-centrale.
''' Permet de définir N centrales Keithley avec leurs paramètres TCP/IP.
''' Chaque centrale a son propre bloc IP/Port/Timeout/Nom + boutons Connecter/Déconnecter.
''' La vérification dialogue (*IDN?, *TST?, voie test) est disponible par centrale.
''' </summary>
Public Class OngletConnexion

    ' ─── Références externes ──────────────────────────────────────────────────

    Public Property Gestionnaire As GestionnaireMultiCentrale
    Public Property Config       As ConfigManager

    ' ─── Contrôles globaux ────────────────────────────────────────────────────

    Private _numNbCentrales  As New NumericUpDown()
    Private _btnAppliquer    As New Button()
    Private _btnConnTout     As New Button()
    Private _btnDeconnTout   As New Button()
    Private _btnSauver       As New Button()
    Private _lblEtatGlobal   As New Label()

    ' Panneau scrollable contenant les blocs par centrale
    Private _pnlBlocs        As New Panel()

    ' Rapport de vérification
    Private _rtbRapport      As New RichTextBox()
    Private _pbarTest        As New ProgressBar()

    ' Contrôles par centrale (indexés par numéro 1..N)
    Private _controlesCentrale As New Dictionary(Of Integer, ControlsCentrale)

    ' ─── Événements ───────────────────────────────────────────────────────────

    Public Event NbCentralesChange(sender As Object, nb As Integer)
    Public Event ConnexionEtablie(sender As Object, centrale As CentraleKeithley)
    Public Event ConnexionFermee(sender As Object, centrale As CentraleKeithley)
    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Structure interne par centrale ───────────────────────────────────────

    Private Class ControlsCentrale
        Public Numero         As Integer
        Public TxtNom         As New TextBox()
        Public TxtIP          As New TextBox()
        Public NumPort        As New NumericUpDown()
        Public NumTimeout     As New NumericUpDown()
        Public BtnConnecter   As New Button()
        Public BtnDeconn      As New Button()
        Public BtnVerifier    As New Button()
        Public BtnDetails     As New Button()
        Public LblEtat        As New Label()
        Public NumVoieTest    As New NumericUpDown()
        Public CmbType        As New ComboBox()
        Public CommandesSCPI  As List(Of CommandeSCPI) = Nothing   ' null = utiliser défauts
    End Class

    ' ─── Construction ─────────────────────────────────────────────────────────

    Public Function ConstruirePanel() As Panel
        Dim pnlGlobal As New Panel() With {.Dock = DockStyle.Fill}

        ' ── Barre de contrôle globale ──
        Dim pnlTop As New Panel() With {.Dock = DockStyle.Top, .Height = 46, .Padding = New Padding(8, 6, 8, 0)}
        Dim flTop  As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight}

        flTop.Controls.Add(New Label() With {
            .Text   = "Nombre de centrales :",
            .AutoSize = True,
            .Font   = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 90, 140),
            .Margin = New Padding(0, 6, 6, 0)
        })

        _numNbCentrales.Minimum = 1
        _numNbCentrales.Maximum = 16
        _numNbCentrales.Value   = 1
        _numNbCentrales.Width   = 60
        _numNbCentrales.Font    = New Font("Consolas", 11, FontStyle.Bold)
        flTop.Controls.Add(_numNbCentrales)

        _btnAppliquer.Text      = "✔ Appliquer"
        _btnAppliquer.BackColor = Color.FromArgb(40, 110, 175)
        _btnAppliquer.ForeColor = Color.White
        _btnAppliquer.FlatStyle = FlatStyle.Flat
        _btnAppliquer.Width     = 110
        _btnAppliquer.Height    = 28
        _btnAppliquer.Margin    = New Padding(8, 2, 0, 0)
        flTop.Controls.Add(_btnAppliquer)

        _btnConnTout.Text      = "🔌 Connecter tout"
        _btnConnTout.BackColor = Color.FromArgb(55, 140, 60)
        _btnConnTout.ForeColor = Color.White
        _btnConnTout.FlatStyle = FlatStyle.Flat
        _btnConnTout.Width     = 140
        _btnConnTout.Height    = 28
        _btnConnTout.Margin    = New Padding(8, 2, 0, 0)
        flTop.Controls.Add(_btnConnTout)

        _btnDeconnTout.Text      = "Déconnecter tout"
        _btnDeconnTout.BackColor = Color.FromArgb(160, 50, 40)
        _btnDeconnTout.ForeColor = Color.White
        _btnDeconnTout.FlatStyle = FlatStyle.Flat
        _btnDeconnTout.Width     = 140
        _btnDeconnTout.Height    = 28
        _btnDeconnTout.Margin    = New Padding(4, 2, 0, 0)
        flTop.Controls.Add(_btnDeconnTout)

        _lblEtatGlobal.AutoSize  = True
        _lblEtatGlobal.ForeColor = Color.Gray
        _lblEtatGlobal.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        _lblEtatGlobal.Text      = "Aucune centrale connectée"
        _lblEtatGlobal.Margin    = New Padding(12, 6, 0, 0)
        flTop.Controls.Add(_lblEtatGlobal)

        ' Bouton Sauvegarder à droite dans la même barre
        _btnSauver.Text      = "💾  Sauvegarder"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80)
        _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat
        _btnSauver.Height    = 28
        _btnSauver.AutoSize  = True
        _btnSauver.Margin    = New Padding(16, 2, 0, 0)
        flTop.Controls.Add(_btnSauver)

        pnlTop.Controls.Add(flTop)

        ' ── Zone des blocs par centrale ──
        _pnlBlocs.Dock      = DockStyle.Fill
        _pnlBlocs.AutoScroll = True
        _pnlBlocs.Padding    = New Padding(6, 4, 6, 4)

        ' ── Rapport de vérification ──
        Dim pnlRapport As New Panel() With {
            .Dock    = DockStyle.Bottom,
            .Height  = 220,
            .Padding = New Padding(0, 0, 0, 4)
        }
        Dim lblRapTitre As New Label() With {
            .Text      = "RAPPORT DE VÉRIFICATION",
            .Dock      = DockStyle.Top,
            .Height    = 20,
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190),
            .Padding   = New Padding(4, 2, 0, 0)
        }
        _pbarTest.Dock    = DockStyle.Top
        _pbarTest.Height  = 6
        _pbarTest.Maximum = 100
        _rtbRapport.Dock        = DockStyle.Fill
        _rtbRapport.ReadOnly    = True
        _rtbRapport.BackColor   = Color.FromArgb(18, 20, 26)
        _rtbRapport.ForeColor   = Color.FromArgb(180, 190, 210)
        _rtbRapport.Font        = New Font("Consolas", 9)
        _rtbRapport.BorderStyle = BorderStyle.None
        _rtbRapport.ScrollBars  = RichTextBoxScrollBars.Vertical
        pnlRapport.Controls.Add(_rtbRapport)
        pnlRapport.Controls.Add(_pbarTest)
        pnlRapport.Controls.Add(lblRapTitre)

        pnlGlobal.Controls.Add(_pnlBlocs)
        pnlGlobal.Controls.Add(pnlRapport)
        pnlGlobal.Controls.Add(pnlTop)

        ' Événements globaux
        AddHandler _numNbCentrales.ValueChanged, AddressOf NbCentrales_Changed
        AddHandler _btnAppliquer.Click,           AddressOf BtnAppliquer_Click
        AddHandler _btnConnTout.Click,            AddressOf BtnConnTout_Click
        AddHandler _btnDeconnTout.Click,          AddressOf BtnDeconnTout_Click
        AddHandler _btnSauver.Click,              AddressOf BtnSauver_Click

        Return pnlGlobal
    End Function

    ' ─── Reconstruction des blocs ─────────────────────────────────────────────

    Private Sub RebuildBlocs()
        _pnlBlocs.Controls.Clear()
        _controlesCentrale.Clear()

        Dim nb = CInt(_numNbCentrales.Value)
        Dim y  = 0

        For i As Integer = 1 To nb
            Dim ctrl = ConstruireBlocCentrale(i)
            _controlesCentrale(i) = ctrl

            Dim bloc = ConstruireCarteCentrale(ctrl)
            bloc.Top    = y
            bloc.Left   = 0
            bloc.Width  = _pnlBlocs.ClientSize.Width - 16
            bloc.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            _pnlBlocs.Controls.Add(bloc)
            y += bloc.Height + 8
        Next

        ' Charger les valeurs depuis le Gestionnaire
        ChargerValeursDepuisGestionnaire()
    End Sub

    Private Function ConstruireBlocCentrale(numero As Integer) As ControlsCentrale
        Dim ctrl As New ControlsCentrale() With {.Numero = numero}

        ctrl.NumPort.Minimum = 1
        ctrl.NumPort.Maximum = 65535
        ctrl.NumPort.Value   = 1394
        ctrl.NumPort.Width   = 80
        ctrl.NumPort.Font    = New Font("Consolas", 10)

        ctrl.NumTimeout.Minimum   = 500
        ctrl.NumTimeout.Maximum   = 30000
        ctrl.NumTimeout.Value     = 3000
        ctrl.NumTimeout.Increment = 500
        ctrl.NumTimeout.Width     = 90
        ctrl.NumTimeout.Font      = New Font("Consolas", 10)

        ctrl.TxtNom.Text  = "Centrale " & numero.ToString()
        ctrl.TxtNom.Width = 130
        ctrl.TxtNom.Font  = New Font("Segoe UI", 9)

        ctrl.TxtIP.Text  = "192.168.0." & (numero + 2).ToString()
        ctrl.TxtIP.Width = 150
        ctrl.TxtIP.Font  = New Font("Consolas", 10)

        ctrl.NumVoieTest.Minimum = 101
        ctrl.NumVoieTest.Maximum = 220
        ctrl.NumVoieTest.Value   = 101
        ctrl.NumVoieTest.Width   = 75
        ctrl.NumVoieTest.Font    = New Font("Consolas", 9)

        ctrl.BtnConnecter.Text      = "🔌 Connecter"
        ctrl.BtnConnecter.BackColor = Color.FromArgb(55, 140, 60)
        ctrl.BtnConnecter.ForeColor = Color.White
        ctrl.BtnConnecter.FlatStyle = FlatStyle.Flat
        ctrl.BtnConnecter.Width     = 110
        ctrl.BtnConnecter.Height    = 26

        ctrl.BtnDeconn.Text      = "Déconnecter"
        ctrl.BtnDeconn.BackColor = Color.FromArgb(160, 50, 40)
        ctrl.BtnDeconn.ForeColor = Color.White
        ctrl.BtnDeconn.FlatStyle = FlatStyle.Flat
        ctrl.BtnDeconn.Width     = 100
        ctrl.BtnDeconn.Height    = 26
        ctrl.BtnDeconn.Enabled   = False

        ctrl.BtnVerifier.Text      = "🔍 Tester"
        ctrl.BtnVerifier.BackColor = Color.FromArgb(40, 110, 175)
        ctrl.BtnVerifier.ForeColor = Color.White
        ctrl.BtnVerifier.FlatStyle = FlatStyle.Flat
        ctrl.BtnVerifier.Width     = 80
        ctrl.BtnVerifier.Height    = 26
        ctrl.BtnVerifier.Enabled   = False

        ' Type de centrale
        ctrl.CmbType.Items.AddRange({"Keithley 2701 Ethernet", "Keithley DAQ6510 Ethernet", "Autre"})
        ctrl.CmbType.SelectedIndex = 0
        ctrl.CmbType.DropDownStyle = ComboBoxStyle.DropDownList
        ctrl.CmbType.Width         = 180
        ctrl.CmbType.Font          = New Font("Segoe UI", 9)
        ' Ajuster le port par défaut selon le type sélectionné
        AddHandler ctrl.CmbType.SelectedIndexChanged,
            Sub(s, e)
                Select Case ctrl.CmbType.SelectedIndex
                    Case 0 ' Keithley 2701
                        If ctrl.NumPort.Value = 5025 Then ctrl.NumPort.Value = 1394
                    Case 1 ' DAQ6510
                        If ctrl.NumPort.Value = 1394 Then ctrl.NumPort.Value = 5025
                End Select
            End Sub

        ' Bouton Détails (visible après Appliquer)
        ctrl.BtnDetails.Text      = "📋 Détails"
        ctrl.BtnDetails.BackColor = Color.FromArgb(70, 80, 110)
        ctrl.BtnDetails.ForeColor = Color.White
        ctrl.BtnDetails.FlatStyle = FlatStyle.Flat
        ctrl.BtnDetails.Width     = 85
        ctrl.BtnDetails.Height    = 26
        ctrl.BtnDetails.Visible   = False   ' affiché après Appliquer

        ctrl.LblEtat.AutoSize  = True
        ctrl.LblEtat.Text      = "⬤ Non connectée"
        ctrl.LblEtat.ForeColor = Color.Gray
        ctrl.LblEtat.Font      = New Font("Segoe UI", 8.5, FontStyle.Bold)
        ctrl.LblEtat.Margin    = New Padding(8, 5, 0, 0)

        ' Événements
        Dim num = numero
        AddHandler ctrl.BtnConnecter.Click, Sub(s, e) BtnConnecter_Click(num)
        AddHandler ctrl.BtnDeconn.Click,    Sub(s, e) BtnDeconn_Click(num)
        AddHandler ctrl.BtnVerifier.Click,  Sub(s, e) BtnVerifier_Click(num)
        AddHandler ctrl.BtnDetails.Click,   Sub(s, e) BtnDetails_Click(num)

        Return ctrl
    End Function

    Private Function ConstruireCarteCentrale(ctrl As ControlsCentrale) As Panel
        ' Conteneur principal avec bordure
        Dim carte As New Panel() With {
            .BorderStyle = BorderStyle.FixedSingle,
            .BackColor   = Color.FromArgb(248, 249, 252),
            .Padding     = New Padding(6, 4, 6, 6)
        }

        ' FlowLayoutPanel vertical — empile les lignes naturellement
        Dim corps As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents  = False,
            .AutoSize      = True,
            .AutoSizeMode  = AutoSizeMode.GrowAndShrink
        }

        ' Ligne 0 : Titre
        corps.Controls.Add(New Label() With {
            .Text      = "CENTRALE N°" & ctrl.Numero.ToString(),
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 90, 140),
            .AutoSize  = True,
            .Margin    = New Padding(0, 0, 0, 4)
        })

        ' Ligne 1 : Type + Nom + Bouton Détails
        Dim fl0 As New FlowLayoutPanel() With {
            .AutoSize      = True,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents  = False,
            .Margin        = New Padding(0, 0, 0, 2)
        }
        fl0.Controls.AddRange({
            New Label() With {.Text = "Type :", .AutoSize = True, .Margin = New Padding(0, 5, 4, 0)},
            ctrl.CmbType,
            New Label() With {.Text = "  Nom :", .AutoSize = True, .Margin = New Padding(8, 5, 4, 0)},
            ctrl.TxtNom,
            ctrl.BtnDetails
        })
        corps.Controls.Add(fl0)

        ' Ligne 2 : IP + Port + Timeout
        Dim fl1 As New FlowLayoutPanel() With {
            .AutoSize      = True,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents  = False,
            .Margin        = New Padding(0, 0, 0, 2)
        }
        fl1.Controls.AddRange({
            New Label() With {.Text = "IP :", .AutoSize = True, .Margin = New Padding(0, 5, 4, 0)},
            ctrl.TxtIP,
            New Label() With {.Text = "  Port :", .AutoSize = True, .Margin = New Padding(8, 5, 4, 0)},
            ctrl.NumPort,
            New Label() With {.Text = "  Timeout (ms) :", .AutoSize = True, .Margin = New Padding(8, 5, 4, 0)},
            ctrl.NumTimeout
        })
        corps.Controls.Add(fl1)

        ' Ligne 3 : Boutons + voie test + état
        Dim fl2 As New FlowLayoutPanel() With {
            .AutoSize      = True,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents  = False,
            .Margin        = New Padding(0, 0, 0, 0)
        }
        fl2.Controls.AddRange({
            ctrl.BtnConnecter,
            ctrl.BtnDeconn,
            ctrl.BtnVerifier,
            New Label() With {.Text = "  Voie test :", .AutoSize = True, .Margin = New Padding(8, 5, 4, 0)},
            ctrl.NumVoieTest,
            ctrl.LblEtat
        })
        corps.Controls.Add(fl2)

        carte.Controls.Add(corps)

        ' Calculer la hauteur après que les contrôles sont ajoutés
        carte.Height = 16 + 24 + 32 + 32 + 32   ' padding + titre + 3 lignes
        Return carte
    End Function

    ' ─── Chargement valeurs depuis Gestionnaire ───────────────────────────────

    Private Sub ChargerValeursDepuisGestionnaire()
        If Gestionnaire Is Nothing Then Return
        For Each kvp In _controlesCentrale
            Dim num   = kvp.Key
            Dim ctrl  = kvp.Value
            Dim c     = Gestionnaire.ObtenirCentrale(num)
            If c Is Nothing Then Continue For
            ctrl.TxtNom.Text     = c.NomAffiche
            ctrl.TxtIP.Text      = c.IPAddress
            ctrl.NumPort.Value   = c.Port
            ctrl.NumTimeout.Value = c.TimeoutMs
            ' Restaurer le type de centrale dans la ComboBox
            Select Case c.TypeCentrale
                Case TypeCentrale.Keithley2701Ethernet : ctrl.CmbType.SelectedIndex = 0
                Case TypeCentrale.DAQ6510Ethernet      : ctrl.CmbType.SelectedIndex = 1
                Case Else                              : ctrl.CmbType.SelectedIndex = 2
            End Select
        Next
    End Sub

    ' ─── Chargement depuis config ─────────────────────────────────────────────

    Public Sub ChargerDepuisConfig()
        Dim nb = Config.GetInt(ConfigManager.SEC_CONNEXION, "NbCentrales", 1)
        _numNbCentrales.Value = nb

        ' Ajuster le gestionnaire
        If Gestionnaire IsNot Nothing Then
            Gestionnaire.DefinirNombreCentrales(nb, Config)
        End If

        RebuildBlocs()
    End Sub

    ' ─── Gestionnaires d'événements ───────────────────────────────────────────

    Private Sub NbCentrales_Changed(sender As Object, e As EventArgs)
        ' Ne rien faire ici — attendre le clic Appliquer
    End Sub

    Private Sub BtnAppliquer_Click(sender As Object, e As EventArgs)
        Dim nb = CInt(_numNbCentrales.Value)

        ' Ajuster le gestionnaire
        If Gestionnaire IsNot Nothing Then
            Gestionnaire.DefinirNombreCentrales(nb, Config)
        End If

        ' Reconstruire les blocs
        RebuildBlocs()

        ' Rendre visible le bouton Détails sur chaque centrale
        For Each kvp In _controlesCentrale
            kvp.Value.BtnDetails.Visible = True
        Next

        ' Mettre à jour les noms dans le gestionnaire et notifier
        AppliquerValeursVersGestionnaire()
        RaiseEvent NbCentralesChange(Me, nb)
        ' MettreAJourNomOnglet est déclenché via NbCentralesChange → ConnexionEtablie
        ' Pour les centrales dont le nom change sans reconnexion, on le force
        For Each kvp In _controlesCentrale
            Dim c = Gestionnaire.ObtenirCentrale(kvp.Key)
            If c IsNot Nothing Then
                RaiseEvent ConnexionEtablie(Me, c)
            End If
        Next
        RaiseEvent StatutChange(Me, nb.ToString() & " centrale(s) configurée(s).", False)
    End Sub

    Private Sub BtnDetails_Click(numero As Integer)
        If Not _controlesCentrale.ContainsKey(numero) Then Return
        Dim ctrl = _controlesCentrale(numero)

        ' Déterminer le type de centrale sélectionné
        Dim typeCentrale As TypeCentrale
        Select Case ctrl.CmbType.SelectedIndex
            Case 0 : typeCentrale = TypeCentrale.Keithley2701Ethernet
            Case 1 : typeCentrale = TypeCentrale.DAQ6510Ethernet
            Case Else : typeCentrale = TypeCentrale.Autre
        End Select

        ' Chercher le formulaire parent pour ShowDialog (OngletConnexion n'est pas un Control)
        Dim parentForm As Form = Nothing
        If _pnlBlocs.FindForm IsNot Nothing Then
            parentForm = _pnlBlocs.FindForm()
        End If

        Using frm As New FormDetailsCentrale(typeCentrale, ctrl.CommandesSCPI)
            frm.ShowDialog(parentForm)
            ctrl.CommandesSCPI = frm.CommandesResultat
        End Using
    End Sub

    Private Sub BtnConnTout_Click(sender As Object, e As EventArgs)
        If Gestionnaire Is Nothing Then Return

        ' Lire les valeurs de l'IHM vers le gestionnaire
        AppliquerValeursVersGestionnaire()

        Dim resultats = Gestionnaire.ConnecterToutes()
        Dim ok = resultats.Values.Where(Function(v) v).Count()
        Dim ko = resultats.Values.Where(Function(v) Not v).Count()

        ' Mettre à jour les états visuels
        For Each kvp In resultats
            MettreAJourEtatCentrale(kvp.Key, kvp.Value)
        Next

        Dim msg = String.Format("{0}/{1} centrale(s) connectée(s)", ok, ok + ko)
        ActualiserEtatGlobal()
        RaiseEvent StatutChange(Me, msg, ko > 0)
        EcrireRapport(msg, If(ko = 0, RapportStyle.OK, RapportStyle.Warn))
    End Sub

    Private Sub BtnDeconnTout_Click(sender As Object, e As EventArgs)
        If Gestionnaire Is Nothing Then Return
        Gestionnaire.DeconnecterToutes()
        For Each kvp In _controlesCentrale
            MettreAJourEtatCentrale(kvp.Key, False)
        Next
        ActualiserEtatGlobal()
        RaiseEvent StatutChange(Me, "Toutes les centrales déconnectées.", False)
        EcrireRapport("Toutes les centrales déconnectées.", RapportStyle.Info)
    End Sub

    Private Sub BtnConnecter_Click(numero As Integer)
        AppliquercentraleVersGestionnaire(numero)
        Dim c = Gestionnaire.ObtenirCentrale(numero)
        If c Is Nothing Then Return

        EcrireRapport(String.Format("--- Connexion Centrale {0} ({1}:{2}) ---",
            numero, c.IPAddress, c.Port), RapportStyle.Section)

        Dim ok = c.Connecter()
        If ok AndAlso _controlesCentrale.ContainsKey(numero) Then
            ' Appliquer les paramètres personnalisés (délai lecture, etc.)
            c.AppliquerCommandesSCPI(_controlesCentrale(numero).CommandesSCPI)
        End If
        MettreAJourEtatCentrale(numero, ok)
        ActualiserEtatGlobal()

        If ok Then
            EcrireRapport("  ✔  Connexion établie.", RapportStyle.OK)
            RaiseEvent ConnexionEtablie(Me, c)
            RaiseEvent StatutChange(Me, "Centrale " & numero & " connectée.", False)
        Else
            EcrireRapport("  ✘  Connexion échouée — vérifier IP/port.", RapportStyle.Erreur)
            RaiseEvent StatutChange(Me, "Centrale " & numero & " : échec connexion.", True)
        End If
    End Sub

    Private Sub BtnDeconn_Click(numero As Integer)
        Dim c = Gestionnaire.ObtenirCentrale(numero)
        If c IsNot Nothing Then
            c.Deconnecter()
            RaiseEvent ConnexionFermee(Me, c)
        End If
        MettreAJourEtatCentrale(numero, False)
        ActualiserEtatGlobal()
        RaiseEvent StatutChange(Me, "Centrale " & numero & " déconnectée.", False)
    End Sub

    Private Sub BtnVerifier_Click(numero As Integer)
        Dim c = Gestionnaire.ObtenirCentrale(numero)
        If c Is Nothing Then Return
        Dim voie = CInt(_controlesCentrale(numero).NumVoieTest.Value)

        _pbarTest.Value = 0
        EcrireRapport(String.Format("═══ VÉRIFICATION CENTRALE {0} — {1} ═══",
            numero, DateTime.Now.ToString("HH:mm:ss")), RapportStyle.Titre)

        Dim worker As New Thread(Sub()
            ' TEST 1 : *IDN?
            EcrireRapport("TEST 1/3 — *IDN?", RapportStyle.Section)
            AvancerBarre(5)
            ' DAQ6510 sur port 5025 peut nécessiter un délai plus long
            If c.EstDAQ6510 Then c.Keithley.DelaiLectureMs = Math.Max(c.Keithley.DelaiLectureMs, 200)
            Dim idn = c.Keithley.Query("*IDN?")
            AvancerBarre(30)
            If idn = "" Then
                EcrireRapport("  ✘  Pas de réponse.", RapportStyle.Erreur)
                FinTests() : Return
            End If
            EcrireRapport("  ✔  " & idn, RapportStyle.Data)

            ' TEST 2 : *TST? — ignoré sur DAQ6510 (peut prendre >10s)
            If Not c.EstDAQ6510 Then
                EcrireRapport("TEST 2/3 — *TST?", RapportStyle.Section)
                AvancerBarre(35)
                Dim tst = c.Keithley.Query("*TST?")
                AvancerBarre(70)
                If tst.Trim() = "0" Then
                    EcrireRapport("  ✔  Self-test OK.", RapportStyle.OK)
                Else
                    EcrireRapport("  ⚠  Code : " & tst, RapportStyle.Warn)
                End If
            Else
                EcrireRapport("TEST 2/3 — *TST? (ignoré sur DAQ6510 — durée > 10s)", RapportStyle.Section)
                EcrireRapport("  ✔  Ignoré intentionnellement.", RapportStyle.OK)
                AvancerBarre(70)
            End If

            ' TEST 3 : lecture voie
            EcrireRapport(String.Format("TEST 3/3 — Lecture voie {0}", voie), RapportStyle.Section)
            If c.EstDAQ6510 Then
                ' Augmenter le timeout le temps du test (mesure peut prendre 2-4s)
                Dim oldTimeout = c.Keithley.ReadTimeout
                c.Keithley.ReadTimeout = 8000
                Try
                    c.Keithley.SendCommand("*RST")
                    Thread.Sleep(500)
                    ' Forcer la fonction Tension DC sur ce canal précis
                    c.Keithley.SendCommand(String.Format(":SENSe:FUNCtion 'VOLTage:DC',(@{0})", voie))
                    Thread.Sleep(200)
                    c.Keithley.SendCommand(":SENSe:VOLTage:DC:RANGe:AUTO ON")
                    Thread.Sleep(100)
                    ' Créer un scan sur cette seule voie et déclencher
                    c.Keithley.SendCommand(String.Format(":ROUTe:SCAN:CREate (@{0})", voie))
                    Thread.Sleep(100)
                    c.Keithley.SendCommand(":INIT")
                    ' Attendre la fin du scan via *OPC? (universel, tous firmware)
                    Dim oldTmo = c.Keithley.ReadTimeout
                    c.Keithley.ReadTimeout = 6000
                    c.Keithley.Query("*OPC?")   ' bloque jusqu'à ce que le scan soit terminé
                    c.Keithley.ReadTimeout = oldTmo
                    Dim mesure = c.Keithley.Query(":FETCh?")
                    ' Vider la file d'erreurs (ignorer 4910 = buffer vide bénin)
                    Dim errStr = c.Keithley.Query(":SYSTem:ERRor?")
                    Dim errLoop = 0
                    Do While errStr <> "" AndAlso Not errStr.StartsWith("+0") AndAlso
                               Not errStr.StartsWith("0,") AndAlso errLoop < 10
                        If Not errStr.Contains("4910") Then
                            EcrireRapport("  ⚠  Erreur instrument : " & errStr, RapportStyle.Warn)
                        End If
                        errStr = c.Keithley.Query(":SYSTem:ERRor?")
                        errLoop += 1
                    Loop
                    AvancerBarre(95)
                    Dim premier = mesure.Trim().Split(","c)(0)
                    Dim valD As Double
                    If premier <> "" AndAlso Double.TryParse(premier,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, valD) Then
                        EcrireRapport(String.Format("  ✔  Voie {0} : {1:F4} V", voie, valD), RapportStyle.OK)
                    Else
                        EcrireRapport("  ⚠  " & If(mesure = "", "Timeout — vérifier DELAI_LECTURE_MS dans Détails SCPI", "Réponse : " & mesure), RapportStyle.Warn)
                    End If
                Finally
                    c.Keithley.ReadTimeout = oldTimeout
                End Try
            Else
                c.Keithley.SendCommand(String.Format(":SENS:FUNC 'VOLT:DC',(@{0})", voie))
                Thread.Sleep(300)
                Dim mesure = c.Keithley.Query(String.Format(":MEAS:VOLT:DC? (@{0})", voie))
                AvancerBarre(95)
                Dim valD As Double
                If mesure <> "" AndAlso Double.TryParse(mesure.Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, valD) Then
                    EcrireRapport(String.Format("  ✔  Voie {0} : {1:F4} V", voie, valD), RapportStyle.OK)
                Else
                    EcrireRapport("  ⚠  " & If(mesure = "", "Timeout", mesure), RapportStyle.Warn)
                End If
            End If
            FinTests()
        End Sub) With {.IsBackground = True}
        worker.Start()
    End Sub

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        AppliquerValeursVersGestionnaire()
        If Gestionnaire IsNot Nothing Then
            Gestionnaire.SauverDansConfig(Config)
        End If
        Config.Set_(ConfigManager.SEC_CONNEXION, "NbCentrales", CInt(_numNbCentrales.Value))
        Try
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "Paramètres de connexion sauvegardés.", False)
            EcrireRapport("Configuration sauvegardée.", RapportStyle.OK)
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical)
        End Try
    End Sub

    ' ─── Synchronisation IHM ↔ Gestionnaire ──────────────────────────────────

    Private Sub AppliquerValeursVersGestionnaire()
        If Gestionnaire Is Nothing Then Return
        For Each kvp In _controlesCentrale
            AppliquercentraleVersGestionnaire(kvp.Key)
        Next
    End Sub

    ''' <summary>
    ''' Applique les valeurs de l'UI vers le gestionnaire et sauvegarde dans Config.
    ''' Appeler depuis FormPrincipal avant toute sauvegarde de config.
    ''' </summary>
    Public Sub SauverVersConfig()
        If Gestionnaire Is Nothing OrElse Config Is Nothing Then Return
        AppliquerValeursVersGestionnaire()
        Gestionnaire.SauverDansConfig(Config)
        Config.Set_(ConfigManager.SEC_CONNEXION, "NbCentrales",
                    CInt(_numNbCentrales.Value))
    End Sub

    Private Sub AppliquercentraleVersGestionnaire(numero As Integer)
        If Gestionnaire Is Nothing Then Return
        Dim c = Gestionnaire.ObtenirCentrale(numero)
        If c Is Nothing Then Return
        Dim ctrl = _controlesCentrale(numero)
        c.Nom       = ctrl.TxtNom.Text.Trim()
        c.IPAddress = ctrl.TxtIP.Text.Trim()
        c.Port      = CInt(ctrl.NumPort.Value)
        c.TimeoutMs = CInt(ctrl.NumTimeout.Value)
        ' Propager le type de centrale
        Select Case ctrl.CmbType.SelectedIndex
            Case 0 : c.TypeCentrale = TypeCentrale.Keithley2701Ethernet
            Case 1 : c.TypeCentrale = TypeCentrale.DAQ6510Ethernet
            Case Else : c.TypeCentrale = TypeCentrale.Autre
        End Select
    End Sub

    Private Sub MettreAJourEtatCentrale(numero As Integer, connecte As Boolean)
        If Not _controlesCentrale.ContainsKey(numero) Then Return
        Dim ctrl = _controlesCentrale(numero)
        Dim invoker = TryCast(ctrl.LblEtat.FindForm(), Form)
        Dim action As Action = Sub()
            ctrl.LblEtat.Text      = If(connecte, "⬤ Connectée", "⬤ Non connectée")
            ctrl.LblEtat.ForeColor = If(connecte, Color.FromArgb(60, 190, 100), Color.Gray)
            ctrl.BtnConnecter.Enabled = Not connecte
            ctrl.BtnDeconn.Enabled    = connecte
            ctrl.BtnVerifier.Enabled  = connecte
        End Sub
        If invoker IsNot Nothing AndAlso invoker.InvokeRequired Then
            invoker.BeginInvoke(action)
        Else
            action()
        End If
    End Sub

    Private Sub ActualiserEtatGlobal()
        If Gestionnaire Is Nothing Then Return
        Dim action As Action = Sub()
            Dim nb  = Gestionnaire.NbConnectees
            Dim tot = Gestionnaire.NbCentrales
            _lblEtatGlobal.Text      = String.Format("⬤ {0}/{1} centrale(s) connectée(s)", nb, tot)
            _lblEtatGlobal.ForeColor = If(nb = 0, Color.Gray,
                                         If(nb = tot, Color.FromArgb(60, 190, 100), Color.DarkOrange))
        End Sub
        If _lblEtatGlobal.InvokeRequired Then
            _lblEtatGlobal.BeginInvoke(action)
        Else
            action()
        End If
    End Sub

    ' ─── Rapport coloré ───────────────────────────────────────────────────────

    Private Enum RapportStyle
        Normal
        Info
        OK
        Warn
        Erreur
        Titre
        Section
        Data
    End Enum

    Private Sub EcrireRapport(texte As String,
                               Optional style As RapportStyle = RapportStyle.Normal)
        Dim couleur As Color
        Select Case style
            Case RapportStyle.OK
                couleur = Color.FromArgb(80, 200, 120)
            Case RapportStyle.Erreur
                couleur = Color.FromArgb(220, 80, 70)
            Case RapportStyle.Warn
                couleur = Color.FromArgb(220, 160, 40)
            Case RapportStyle.Titre
                couleur = Color.FromArgb(130, 160, 220)
            Case RapportStyle.Section
                couleur = Color.FromArgb(90, 150, 210)
            Case RapportStyle.Data
                couleur = Color.FromArgb(180, 220, 255)
            Case RapportStyle.Info
                couleur = Color.FromArgb(150, 160, 185)
            Case Else
                couleur = Color.FromArgb(180, 190, 210)
        End Select

        If _rtbRapport.InvokeRequired Then
            _rtbRapport.BeginInvoke(Sub() AppendLine(texte, couleur))
        Else
            AppendLine(texte, couleur)
        End If
    End Sub

    Private Sub AppendLine(texte As String, couleur As Color)
        _rtbRapport.SelectionStart  = _rtbRapport.TextLength
        _rtbRapport.SelectionLength = 0
        _rtbRapport.SelectionColor  = couleur
        _rtbRapport.AppendText(texte & Environment.NewLine)
        _rtbRapport.SelectionColor  = _rtbRapport.ForeColor
        _rtbRapport.ScrollToCaret()
    End Sub

    Private Sub AvancerBarre(v As Integer)
        If _pbarTest.InvokeRequired Then
            _pbarTest.BeginInvoke(Sub() _pbarTest.Value = v)
        Else
            _pbarTest.Value = v
        End If
    End Sub

    Private Sub FinTests()
        AvancerBarre(100)
        EcrireRapport("═══ FIN ═══", RapportStyle.Titre)
    End Sub

End Class
