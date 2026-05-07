Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports System.Diagnostics

''' <summary>
''' Onglet Rapport — génère et affiche les rapports PDF d'essai.
''' Permet de générer manuellement un rapport, de consulter les anciens,
''' et de définir le dossier de destination.
''' </summary>
Public Class OngletRapport

    Public Property Config        As ConfigManager
    Public Property Generateur    As GenerateurRapport

    Private _txtDossier     As New TextBox()
    Private _btnParcourir   As New Button()
    Private _txtPython      As New TextBox()
    Private _btnPython      As New Button()
    Private _txtScript      As New TextBox()
    Private _btnScript      As New Button()
    Private _txtOperateur   As New TextBox()
    Private _txtLabo        As New TextBox()
    Private _txtProjet      As New TextBox()
    Private _txtNotes       As New RichTextBox()
    Private _txtLogo        As New TextBox()
    Private _btnLogo        As New Button()
    Private _btnSupprimerLogo As New Button()
    Private _lblLogoApercu  As New PictureBox()
    Private _cmbPolice      As New ComboBox()
    Private _chkSauverGraphique As New CheckBox()
    Private _btnSauver        As New Button()
    Private _lblSauveStatut   As New Label()
    Private _btnGenerer     As New Button()
    Private _btnOuvrir      As New Button()
    Private _btnOuvrirDoss  As New Button()
    Private _lstRapports    As New ListBox()
    Private _lblStatut      As New Label()
    Private _lblInfo        As New Label()
    Private _chkAutoGenerer As New CheckBox()

    Public ReadOnly Property AutoGenerer As Boolean
        Get
            Return _chkAutoGenerer.Checked
        End Get
    End Property

    Public ReadOnly Property DossierRapports As String
        Get
            Return _txtDossier.Text.Trim()
        End Get
    End Property

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Construction ─────────────────────────────────────────────────────────
    Public Function ConstruirePanel() As Panel
        Dim scroll As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True}
        Dim pnl As New TableLayoutPanel() With {
            .Dock        = DockStyle.Top,
            .AutoSize    = True,
            .ColumnCount = 2,
            .Padding     = New Padding(14, 12, 14, 10)
        }
        pnl.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200))
        pnl.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        ' ── Dossier destination ──
        AjouterSep(pnl, "DOSSIER DES RAPPORTS")
        _txtDossier.Font  = New Font("Consolas", 9)
        _txtDossier.Width = 360
        _btnParcourir.Text      = "📁 Parcourir..." : _btnParcourir.Width = 110 : _btnParcourir.Height = 28
        _btnParcourir.FlatStyle = FlatStyle.Flat
        _btnParcourir.BackColor = Color.FromArgb(55, 60, 75) : _btnParcourir.ForeColor = Color.White
        Dim pnlDoss As New FlowLayoutPanel() With {.AutoSize = True}
        pnlDoss.Controls.AddRange({_txtDossier, _btnParcourir})
        AjouterLigne(pnl, "Dossier :", pnlDoss)

        ' ── Chemin Python (optionnel) ──
        AjouterSep(pnl, "CHEMIN PYTHON (facultatif si Python est dans le PATH)")
        _txtPython.Font      = New Font("Consolas", 9) : _txtPython.Width = 360
        _txtPython.ForeColor = Color.FromArgb(100, 110, 140)
        Dim lblPyHint As New Label() With {
            .Text      = "Laissez vide pour utiliser Python du PATH (python3, python, py)",
            .AutoSize  = True, .Font = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.FromArgb(120, 130, 150)}
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(lblPyHint)
        _btnPython.Text      = "🔍 Parcourir..." : _btnPython.Width = 100 : _btnPython.Height = 28
        _btnPython.FlatStyle = FlatStyle.Flat
        _btnPython.BackColor = Color.FromArgb(55, 60, 75) : _btnPython.ForeColor = Color.White
        Dim pnlPy As New FlowLayoutPanel() With {.AutoSize = True}
        pnlPy.Controls.AddRange({_txtPython, _btnPython})
        AjouterLigne(pnl, "Exécutable Python :", pnlPy)

        ' Chemin du script
        _txtScript.Font      = New Font("Consolas", 9) : _txtScript.Width = 360
        _txtScript.ForeColor = Color.FromArgb(100, 110, 140)
        Dim lblScriptHint As New Label() With {
            .Text      = "Laissez vide : cherche generer_rapport.py dans le dossier de l'exe",
            .AutoSize  = True, .Font = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.FromArgb(120, 130, 150)}
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(lblScriptHint)
        _btnScript.Text      = "🔍 Parcourir..." : _btnScript.Width = 100 : _btnScript.Height = 28
        _btnScript.FlatStyle = FlatStyle.Flat
        _btnScript.BackColor = Color.FromArgb(55, 60, 75) : _btnScript.ForeColor = Color.White
        Dim pnlScript As New FlowLayoutPanel() With {.AutoSize = True}
        pnlScript.Controls.AddRange({_txtScript, _btnScript})
        AjouterLigne(pnl, "Script generer_rapport.py :", pnlScript)

        ' ── Personnalisation ──
        AjouterSep(pnl, "PERSONNALISATION DU RAPPORT")

        _txtOperateur.Font = New Font("Segoe UI", 9) : _txtOperateur.Width = 280
        AjouterLigne(pnl, "Opérateur :", _txtOperateur)

        _txtLabo.Font = New Font("Segoe UI", 9) : _txtLabo.Width = 280
        AjouterLigne(pnl, "Laboratoire :", _txtLabo)

        _txtProjet.Font = New Font("Segoe UI", 9) : _txtProjet.Width = 280
        AjouterLigne(pnl, "Projet / Essai :", _txtProjet)

        _txtNotes.Font = New Font("Segoe UI", 9)
        _txtNotes.Width = 380 : _txtNotes.Height = 60
        _txtNotes.ScrollBars = RichTextBoxScrollBars.Vertical
        AjouterLigne(pnl, "Notes libres :", _txtNotes)

        ' Police
        _cmbPolice.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbPolice.Width = 180
        _cmbPolice.Items.AddRange({"Helvetica (défaut)", "Times-Roman", "Courier"})
        _cmbPolice.SelectedIndex = 0
        AjouterLigne(pnl, "Police :", _cmbPolice)

        ' Logo
        AjouterSep(pnl, "LOGO (PNG ou JPG, sera affiché sur la page de couverture)")
        _txtLogo.Font = New Font("Consolas", 8.5) : _txtLogo.Width = 280
        _txtLogo.ReadOnly = True : _txtLogo.ForeColor = Color.FromArgb(80, 100, 140)
        _btnLogo.Text = "📁 Choisir..." : _btnLogo.Width = 90 : _btnLogo.Height = 28
        _btnLogo.FlatStyle = FlatStyle.Flat
        _btnLogo.BackColor = Color.FromArgb(55, 60, 75) : _btnLogo.ForeColor = Color.White
        _btnSupprimerLogo.Text = "✕" : _btnSupprimerLogo.Width = 28 : _btnSupprimerLogo.Height = 28
        _btnSupprimerLogo.FlatStyle = FlatStyle.Flat
        _btnSupprimerLogo.BackColor = Color.FromArgb(140, 40, 40) : _btnSupprimerLogo.ForeColor = Color.White
        Dim pnlLogo As New FlowLayoutPanel() With {.AutoSize = True}
        pnlLogo.Controls.AddRange({_txtLogo, _btnLogo, _btnSupprimerLogo})
        AjouterLigne(pnl, "Fichier logo :", pnlLogo)
        _lblLogoApercu.Width = 120 : _lblLogoApercu.Height = 60
        _lblLogoApercu.SizeMode = PictureBoxSizeMode.Zoom
        _lblLogoApercu.BorderStyle = BorderStyle.FixedSingle
        _lblLogoApercu.BackColor = Color.WhiteSmoke
        AjouterLigne(pnl, "Aperçu :", _lblLogoApercu)

        ' ── Options ──
        AjouterSep(pnl, "OPTIONS")
        _chkAutoGenerer.Text    = "Générer automatiquement à chaque arrêt d'acquisition"
        _chkAutoGenerer.Checked = True
        _chkAutoGenerer.AutoSize = True
        _chkAutoGenerer.Font    = New Font("Segoe UI", 9)
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(_chkAutoGenerer)

        _chkSauverGraphique.Text    = "Inclure le graphique (PNG) dans le rapport en fin d'acquisition"
        _chkSauverGraphique.Checked = True
        _chkSauverGraphique.AutoSize = True
        _chkSauverGraphique.Font    = New Font("Segoe UI", 9)
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(_chkSauverGraphique)

        ' ── Génération manuelle ──
        AjouterSep(pnl, "GÉNÉRER UN RAPPORT")
        _lblInfo.Text      = "Le rapport reprend toute la configuration active : connexion, voies, " &
                             "sorties, acquisition, calculs, chronogramme et règles conditionnelles."
        _lblInfo.AutoSize  = True
        _lblInfo.Font      = New Font("Segoe UI", 8.5, FontStyle.Italic)
        _lblInfo.ForeColor = Color.FromArgb(90, 100, 130)
        _lblInfo.MaximumSize = New Size(520, 0)
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(_lblInfo)

        _btnGenerer.Text      = "📄 Générer le rapport maintenant"
        _btnGenerer.BackColor = Color.FromArgb(40, 110, 175)
        _btnGenerer.ForeColor = Color.White
        _btnGenerer.FlatStyle = FlatStyle.Flat
        _btnGenerer.Width     = 230 : _btnGenerer.Height = 28
        _btnGenerer.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        _btnGenerer.Margin    = New Padding(0, 4, 8, 0)
        _btnOuvrir.Text       = "🔍 Ouvrir le dernier rapport"
        _btnOuvrir.BackColor  = Color.FromArgb(55, 60, 75)
        _btnOuvrir.ForeColor  = Color.White
        _btnOuvrir.FlatStyle  = FlatStyle.Flat
        _btnOuvrir.Width      = 190 : _btnOuvrir.Height = 28
        _btnOuvrir.Margin     = New Padding(0, 4, 0, 0)
        Dim pnlBtns As New FlowLayoutPanel() With {.AutoSize = True}
        pnlBtns.Controls.AddRange({_btnGenerer, _btnOuvrir})
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(pnlBtns)

        ' Statut
        _lblStatut.AutoSize  = True
        _lblStatut.Font      = New Font("Segoe UI", 9, FontStyle.Italic)
        _lblStatut.ForeColor = Color.FromArgb(60, 140, 60)
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(_lblStatut)

        ' ── Liste des rapports existants ──
        AjouterSep(pnl, "RAPPORTS EXISTANTS")
        _lstRapports.Font              = New Font("Consolas", 9)
        _lstRapports.Height            = 200
        _lstRapports.MinimumSize       = New Size(600, 200)
        _lstRapports.BackColor         = Color.White
        _lstRapports.ForeColor         = Color.FromArgb(30, 40, 70)
        _lstRapports.BorderStyle       = BorderStyle.FixedSingle
        _lstRapports.HorizontalScrollbar = True
        _btnOuvrirDoss.Text      = "📂 Ouvrir le dossier"
        _btnOuvrirDoss.FlatStyle = FlatStyle.Flat
        _btnOuvrirDoss.BackColor = Color.FromArgb(45, 50, 65)
        _btnOuvrirDoss.ForeColor = Color.Silver
        _btnOuvrirDoss.Width     = 155 : _btnOuvrirDoss.Height = 28
        _btnOuvrirDoss.Margin    = New Padding(0, 4, 6, 0)

        Dim btnActualiser As New Button() With {
            .Text      = "🔄 Actualiser",
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(45, 50, 65),
            .ForeColor = Color.Silver,
            .Width     = 120, .Height = 28,
            .Margin    = New Padding(0, 4, 0, 0)}
        AddHandler btnActualiser.Click, Sub(s, e) ActualiserListeRapports()

        ' Liste
        pnl.Controls.Add(New Label() With {.Height = 2})
        pnl.Controls.Add(_lstRapports)
        pnl.SetColumnSpan(_lstRapports, 2)

        ' Boutons sous la liste
        Dim pnlBtnsRapport As New FlowLayoutPanel() With {
            .AutoSize      = True,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents  = False}
        pnlBtnsRapport.Controls.AddRange({_btnOuvrirDoss, btnActualiser})
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(pnlBtnsRapport)

        ' Supprimer la section SAUVEGARDE du TableLayoutPanel — elle sera en bas fixe
        scroll.Controls.Add(pnl)

        ' ── Barre Sauvegarder fixe en bas (hors scroll) ──────────────────────
        _btnSauver.Text      = "💾 Sauvegarder les paramètres du rapport"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80)
        _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat
        _btnSauver.Width     = 280 : _btnSauver.Height = 28
        _btnSauver.Font      = New Font("Segoe UI", 8.5)
        _lblSauveStatut.AutoSize  = True
        _lblSauveStatut.Font      = New Font("Segoe UI", 8.5, FontStyle.Italic)
        _lblSauveStatut.ForeColor = Color.FromArgb(60, 140, 60)
        _lblSauveStatut.Margin    = New Padding(12, 6, 0, 0)

        Dim pnlFixeBasRapport As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Bottom,
            .Height        = 50,
            .Padding       = New Padding(8, 10, 8, 0),
            .BackColor     = Color.FromArgb(245, 247, 252)
        }
        pnlFixeBasRapport.Controls.AddRange({_btnSauver, _lblSauveStatut})

        ' Le scroll prend tout l'espace — bouton Sauvegarder géré par FormPrincipal

        AddHandler _btnParcourir.Click,  AddressOf Parcourir_Click
        AddHandler _btnPython.Click,     AddressOf ParcourirPython_Click
        AddHandler _btnScript.Click,       AddressOf ParcourirScript_Click
        AddHandler _btnSauver.Click,       AddressOf BtnSauver_Click
        AddHandler _btnLogo.Click,         AddressOf ParcourirLogo_Click
        AddHandler _btnSupprimerLogo.Click, Sub(s, e) SupprimerLogo()
        AddHandler _btnGenerer.Click,    AddressOf Generer_Click
        AddHandler _btnOuvrir.Click,     AddressOf Ouvrir_Click
        AddHandler _btnOuvrirDoss.Click, AddressOf OuvrirDossier_Click
        AddHandler _lstRapports.DoubleClick, AddressOf OuvrirRapportSelectionne
        ' Actualiser la liste quand le dossier change
        AddHandler _txtDossier.TextChanged, Sub(s, e) ActualiserListeRapports()

        Return scroll
    End Function

    ' ─── Config ───────────────────────────────────────────────────────────────
    Public Sub ChargerDepuisConfig()
        _txtDossier.Text = Config.Get_(ConfigManager.SEC_CSV, "DossierRapports",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Thermopilot", "Rapports"))
        _chkAutoGenerer.Checked = Config.GetBool(ConfigManager.SEC_CSV, "AutoGenererRapport", True)
        _txtPython.Text = Config.Get_(ConfigManager.SEC_CSV, "CheminPython", "")
        _txtScript.Text     = Config.Get_(ConfigManager.SEC_CSV, "CheminScript",  "")
        _txtOperateur.Text  = Config.Get_(ConfigManager.SEC_CSV, "RapportOperateur", "Adrien")
        _txtLabo.Text       = Config.Get_(ConfigManager.SEC_CSV, "RapportLabo",      "IRDL PTR4 — Lorient")
        _txtProjet.Text     = Config.Get_(ConfigManager.SEC_CSV, "RapportProjet",    "")
        _txtNotes.Text      = Config.Get_(ConfigManager.SEC_CSV, "RapportNotes",     "")
        _txtLogo.Text       = Config.Get_(ConfigManager.SEC_CSV, "RapportLogo",      "")
        _chkSauverGraphique.Checked = Config.GetBool(ConfigManager.SEC_CSV, "RapportSauverGraphique", True)
        Dim idxPolice = Config.GetInt(ConfigManager.SEC_CSV, "RapportPolice", 0)
        If _cmbPolice.Items.Count > 0 Then _cmbPolice.SelectedIndex = Math.Max(0, Math.Min(2, idxPolice))
        If _txtLogo.Text <> "" AndAlso File.Exists(_txtLogo.Text) Then
            Try : _lblLogoApercu.Image = Image.FromFile(_txtLogo.Text) : Catch : End Try
        End If
        ActualiserListeRapports()
    End Sub

    Public Sub SauverDansConfig()
        Config.Set_(ConfigManager.SEC_CSV, "DossierRapports",      _txtDossier.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "AutoGenererRapport",    _chkAutoGenerer.Checked)
        Config.Set_(ConfigManager.SEC_CSV, "CheminPython",          _txtPython.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "CheminScript",            _txtScript.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportOperateur",        _txtOperateur.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportLabo",             _txtLabo.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportProjet",           _txtProjet.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportNotes",            _txtNotes.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportLogo",             _txtLogo.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "RapportSauverGraphique",  _chkSauverGraphique.Checked)
        Config.Set_(ConfigManager.SEC_CSV, "RapportPolice",           _cmbPolice.SelectedIndex)
    End Sub

    ' ─── Génération ───────────────────────────────────────────────────────────

    ''' <summary>
    ''' Prépare un InfosRapport en lisant toutes les valeurs UI (à appeler depuis le thread UI).
    ''' horodatage : timestamp partagé avec le CSV et le graphique (ex: "20260506-105802")
    ''' </summary>
    Public Function PreparerInfos(Optional suffixe As String = "",
                                  Optional horodatage As String = "") As InfosRapport
        Dim dossier = _txtDossier.Text.Trim()
        Try : Directory.CreateDirectory(dossier) : Catch : End Try
        Dim ts = If(horodatage <> "", horodatage, DateTime.Now.ToString("yyyyMMdd-HHmmss"))
        Dim nomFichier = String.Format("Rapport_{0}{1}.pdf",
            ts,
            If(suffixe <> "", "_" & suffixe, ""))
        Return New InfosRapport() With {
            .Chemin      = Path.Combine(dossier, nomFichier),
            .NomFichier  = nomFichier,
            .Python      = _txtPython.Text.Trim(),
            .Script      = _txtScript.Text.Trim(),
            .Operateur   = Operateur,
            .Laboratoire = Laboratoire,
            .Projet      = Projet,
            .Notes       = Notes,
            .CheminLogo  = CheminLogo,
            .NomPolice   = NomPolice
        }
    End Function

    Public Class InfosRapport
        Public Property Chemin      As String = ""
        Public Property NomFichier  As String = ""
        Public Property Python      As String = ""
        Public Property Script      As String = ""
        Public Property Operateur   As String = ""
        Public Property Laboratoire As String = ""
        Public Property Projet      As String = ""
        Public Property Notes       As String = ""
        Public Property CheminLogo  As String = ""
        Public Property NomPolice   As String = ""
    End Class

    ''' <summary>
    ''' Génère le rapport à partir d'InfosRapport pré-calculées (thread-safe).
    ''' Peut être appelé depuis Task.Run — ne touche aux contrôles UI que via BeginInvoke.
    ''' </summary>
    Public Function GenererRapportAvecInfos(infos As InfosRapport) As String
        If Generateur Is Nothing Then Return ""

        Dim setStatut = Sub(texte As String, couleur As Color)
            If _lblStatut.InvokeRequired Then
                _lblStatut.BeginInvoke(New Action(Sub()
                    _lblStatut.Text = texte : _lblStatut.ForeColor = couleur
                End Sub))
            Else
                _lblStatut.Text = texte : _lblStatut.ForeColor = couleur
            End If
        End Sub

        setStatut("Génération en cours…", Color.FromArgb(40, 90, 170))

        If infos.Python <> "" Then Generateur.CheminPython = infos.Python
        If infos.Script <> "" Then Generateur.CheminScript = infos.Script
        Generateur.Operateur   = infos.Operateur
        Generateur.Laboratoire = infos.Laboratoire
        Generateur.Projet      = infos.Projet
        Generateur.Notes       = infos.Notes
        Generateur.CheminLogo  = infos.CheminLogo
        Generateur.NomPolice   = infos.NomPolice

        Dim resultat = Generateur.Generer(infos.Chemin)
        If resultat <> "" Then
            setStatut("✔ Rapport généré : " & infos.NomFichier, Color.FromArgb(30, 120, 50))
            If _lstRapports.InvokeRequired Then
                _lstRapports.BeginInvoke(New Action(AddressOf ActualiserListeRapports))
            Else
                ActualiserListeRapports()
            End If
            RaiseEvent StatutChange(Me, "Rapport PDF généré → " & infos.NomFichier, False)
        Else
            setStatut("⚠ Échec de la génération.", Color.DarkRed)
        End If
        Return resultat
    End Function

    ''' <summary>Génération synchrone depuis le thread UI (bouton manuel).</summary>
    Public Function GenererRapport(Optional suffixe As String = "") As String
        Dim infos = PreparerInfos(suffixe)
        Return GenererRapportAvecInfos(infos)
    End Function

    Private Sub ActualiserListeRapports()
        _lstRapports.Items.Clear()
        Dim dossier = _txtDossier.Text.Trim()
        If Not Directory.Exists(dossier) Then Return
        ' Chercher tous les PDF (pas seulement Rapport_*.pdf)
        Dim tous = Directory.GetFiles(dossier, "*.pdf")
        Dim fichiers = tous.OrderByDescending(Function(f) File.GetLastWriteTime(f)).Take(50).ToArray()
        For Each f In fichiers
            _lstRapports.Items.Add(Path.GetFileName(f))
        Next
    End Sub

    ' ─── Gestionnaires ────────────────────────────────────────────────────────
    Public ReadOnly Property Operateur      As String
        Get
            Return _txtOperateur.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property Laboratoire    As String
        Get
            Return _txtLabo.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property Projet         As String
        Get
            Return _txtProjet.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property Notes          As String
        Get
            Return _txtNotes.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property CheminLogo     As String
        Get
            Return _txtLogo.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property NomPolice      As String
        Get
            Select Case _cmbPolice.SelectedIndex
                Case 1 : Return "Times-Roman"
                Case 2 : Return "Courier"
                Case Else : Return "Helvetica"
            End Select
        End Get
    End Property
    Public ReadOnly Property SauverGraphique As Boolean
        Get
            Return _chkSauverGraphique.Checked
        End Get
    End Property

    Public ReadOnly Property CheminPython As String
        Get
            Return _txtPython.Text.Trim()
        End Get
    End Property

    ''' <summary>Panneau bouton Sauvegarder a placer dans le TabPage (DockStyle.Bottom).</summary>
    Public Function ConstruirePanneauSauvegarde() As Panel
        Dim pnl As New FlowLayoutPanel() With {
            .Dock      = DockStyle.Bottom,
            .Height    = 40,
            .Padding   = New Padding(8, 6, 8, 6),
            .BackColor = Color.FromArgb(245, 247, 252)
        }
        pnl.Controls.AddRange({_btnSauver, _lblSauveStatut})
        Return pnl
    End Function

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        SauverDansConfig()
        Config.Sauvegarder()
        _lblSauveStatut.Text = "✔ Sauvegardé"
        Dim t As New System.Windows.Forms.Timer() With {.Interval = 2500}
        AddHandler t.Tick, Sub(s, ev)
            _lblSauveStatut.Text = ""
            t.Stop()
        End Sub
        t.Start()
        RaiseEvent StatutChange(Me, "Paramètres rapport sauvegardés.", False)
    End Sub

    Private Sub ParcourirLogo_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog() With {
            .Title  = "Sélectionner le logo",
            .Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|Tous|*.*"}
            If dlg.ShowDialog() = DialogResult.OK Then
                _txtLogo.Text = dlg.FileName
                Try
                    _lblLogoApercu.Image = Image.FromFile(dlg.FileName)
                Catch
                    _lblLogoApercu.Image = Nothing
                End Try
            End If
        End Using
    End Sub

    Private Sub SupprimerLogo()
        _txtLogo.Text     = ""
        _lblLogoApercu.Image = Nothing
    End Sub

    Private Sub ParcourirScript_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog() With {
            .Title  = "Sélectionner generer_rapport.py",
            .Filter = "Script Python|generer_rapport.py;*.py|Tous|*.*"}
            If dlg.ShowDialog() = DialogResult.OK Then
                _txtScript.Text = dlg.FileName
                If Generateur IsNot Nothing Then
                    Generateur.CheminScript = dlg.FileName
                End If
            End If
        End Using
    End Sub

    Private Sub ParcourirPython_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog() With {
            .Title  = "Sélectionner python.exe",
            .Filter = "python.exe|python.exe;python3.exe;py.exe|Tous|*.*"}
            If dlg.ShowDialog() = DialogResult.OK Then
                _txtPython.Text = dlg.FileName
                If Generateur IsNot Nothing Then
                    Generateur.CheminPython = dlg.FileName
                End If
            End If
        End Using
    End Sub

    Private Sub Parcourir_Click(sender As Object, e As EventArgs)
        Using dlg As New FolderBrowserDialog() With {
            .Description  = "Dossier de destination des rapports PDF",
            .SelectedPath = _txtDossier.Text}
            If dlg.ShowDialog() = DialogResult.OK Then
                _txtDossier.Text = dlg.SelectedPath
                ActualiserListeRapports()
            End If
        End Using
    End Sub

    Private Sub Generer_Click(sender As Object, e As EventArgs)
        GenererRapport()
    End Sub

    Private Sub Ouvrir_Click(sender As Object, e As EventArgs)
        Dim dossier = _txtDossier.Text.Trim()
        If Not Directory.Exists(dossier) Then Return
        Dim tousF = Directory.GetFiles(dossier, "Rapport_*.pdf")
            Dim dernierFichier = tousF.OrderByDescending(Function(f) f).FirstOrDefault()
        If dernierFichier IsNot Nothing Then
            Try : Process.Start(dernierFichier) : Catch ex As Exception
                MessageBox.Show("Impossible d'ouvrir le PDF : " & ex.Message)
            End Try
        Else
            MessageBox.Show("Aucun rapport trouvé dans ce dossier.")
        End If
    End Sub

    Private Sub OuvrirDossier_Click(sender As Object, e As EventArgs)
        Try
            Directory.CreateDirectory(_txtDossier.Text.Trim())
            Process.Start("explorer.exe", _txtDossier.Text.Trim())
        Catch ex As Exception
            MessageBox.Show("Impossible d'ouvrir le dossier : " & ex.Message)
        End Try
    End Sub

    Private Sub OuvrirRapportSelectionne(sender As Object, e As EventArgs)
        If _lstRapports.SelectedItem Is Nothing Then Return
        Dim chemin = Path.Combine(_txtDossier.Text.Trim(), _lstRapports.SelectedItem.ToString())
        Try : Process.Start(chemin) : Catch ex As Exception
            MessageBox.Show("Impossible d'ouvrir : " & ex.Message)
        End Try
    End Sub

    ' ─── Layout helpers ───────────────────────────────────────────────────────
    Private Sub AjouterSep(pnl As TableLayoutPanel, titre As String)
        Dim lbl As New Label() With {
            .Text = titre, .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190), .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 14, 0, 4)}
        pnl.Controls.Add(lbl) : pnl.SetColumnSpan(lbl, 2)
    End Sub

    Private Sub AjouterLigne(pnl As TableLayoutPanel, etiquette As String, ctrl As Control)
        pnl.Controls.Add(New Label() With {
            .Text = etiquette, .AutoSize = True,
            .ForeColor = Color.FromArgb(140, 150, 170),
            .Font = New Font("Segoe UI", 9),
            .Margin = New Padding(0, 6, 8, 0),
            .TextAlign = ContentAlignment.MiddleRight})
        ctrl.Margin = New Padding(0, 2, 0, 2)
        pnl.Controls.Add(ctrl)
    End Sub

End Class
