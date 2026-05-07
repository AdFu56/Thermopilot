Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.IO
Imports System.Reflection

''' <summary>
''' Splash screen affiché au démarrage.
''' Personnalisation dans les constantes en haut de la classe :
'''   - TITRE          : nom de l'application
'''   - SOUS_TITRE     : ligne de description
'''   - VERSION        : numéro de version
'''   - NOM_LABO       : nom du laboratoire
'''   - CHEMIN_LOGO    : chemin vers le fichier image du logo (png/jpg)
'''                      → laisser vide pour afficher uniquement le texte
'''   - DUREE_MS       : durée d'affichage en millisecondes
'''   - COULEUR_FOND   : couleur de fond du splash
'''   - COULEUR_TITRE  : couleur du titre
''' </summary>
Public Class FormSplash
    Inherits Form

    ' ═══════════════════════════════════════════════════════════
    '  PERSONNALISATION — modifiez ces valeurs
    ' ═══════════════════════════════════════════════════════════

    Private Const TITRE       As String = "Thermopilot"
    Private Const SOUS_TITRE  As String = "Acquisition multi-centrale & Pilotage"
    Private Const VERSION     As String = "v2.0"
    Private Const NOM_LABO    As String = "IRDL PTR4"
    Private Const NOM_AUTEUR  As String = "Adrien Fuentes"
    Private Const CHEMIN_LOGO As String = ""   ' ex : "C:\MonLabo\logo.png"
    Private Const DUREE_MS    As Integer = 3000

    Private Shared ReadOnly COULEUR_FOND  As Color = Color.FromArgb(22, 26, 38)
    Private Shared ReadOnly COULEUR_TITRE As Color = Color.FromArgb(100, 160, 230)
    Private Shared ReadOnly COULEUR_TEXTE As Color = Color.FromArgb(200, 210, 230)
    Private Shared ReadOnly COULEUR_LABO  As Color = Color.FromArgb(140, 160, 190)
    Private Shared ReadOnly COULEUR_BARRE As Color = Color.FromArgb(55, 130, 200)

    ' ═══════════════════════════════════════════════════════════

    Private _logo          As Image = Nothing
    Private _timer         As New Timer()
    Private _progresseValue As Integer = 0
    Private _timerProgres  As New Timer()

    Public Sub New()
        ' Pas de bordure, centré, au premier plan
        Me.FormBorderStyle = FormBorderStyle.None
        Me.StartPosition   = FormStartPosition.CenterScreen
        Me.Size            = New Size(600, 340)
        Me.BackColor       = COULEUR_FOND
        Me.TopMost         = True
        Me.Cursor          = Cursors.WaitCursor
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer, True)

        ' Charger le logo si spécifié
        If CHEMIN_LOGO <> "" AndAlso File.Exists(CHEMIN_LOGO) Then
            Try
                _logo = Image.FromFile(CHEMIN_LOGO)
            Catch
            End Try
        End If

        ' Timer de fermeture
        _timer.Interval = DUREE_MS
        _timer.Start()
        AddHandler _timer.Tick, Sub(s, e)
            _timer.Stop()
            _timerProgres.Stop()
            Me.Close()
        End Sub

        ' Barre de progression animée
        _timerProgres.Interval = DUREE_MS \ 100   ' 100 pas
        _timerProgres.Start()
        AddHandler _timerProgres.Tick, Sub(s, e)
            If _progresseValue < 100 Then
                _progresseValue += 1
                Invalidate(New Rectangle(30, Height - 28, Width - 60, 10))
            End If
        End Sub

        ' Clic pour fermer plus tôt
        AddHandler Me.MouseClick, Sub(s, e)
            _timer.Stop()
            _timerProgres.Stop()
            Me.Close()
        End Sub
    End Sub

    ' ─── Rendu ────────────────────────────────────────────────────────────────

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g  = e.Graphics
        Dim w  = Width
        Dim h  = Height
        g.SmoothingMode    = SmoothingMode.AntiAlias
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        ' ── Fond dégradé ──
        Using br = New LinearGradientBrush(
            New Point(0, 0), New Point(0, h),
            COULEUR_FOND,
            Color.FromArgb(32, 38, 55))
            g.FillRectangle(br, 0, 0, w, h)
        End Using

        ' ── Bordure fine ──
        Using pen = New Pen(Color.FromArgb(60, 80, 120), 1.5)
            g.DrawRectangle(pen, 1, 1, w - 3, h - 3)
        End Using

        ' ── Ligne décorative haut ──
        Using br = New LinearGradientBrush(
            New Point(0, 0), New Point(w, 0),
            Color.Transparent, COULEUR_BARRE)
            Dim blend As New Blend()
            blend.Positions  = {0.0F, 0.5F, 1.0F}
            blend.Factors    = {0.0F, 1.0F, 0.0F}
            br.Blend         = blend
            g.FillRectangle(br, 0, 0, w, 3)
        End Using

        Dim xTexte = 30
        Dim yBase  = 30

        ' ── Logo (si disponible) ──
        If _logo IsNot Nothing Then
            Dim logoH  = 90
            Dim logoW  = CInt(_logo.Width * (logoH / CDbl(_logo.Height)))
            logoW      = Math.Min(logoW, 160)
            Dim logoX  = w - logoW - 30
            Dim logoY  = 30
            g.DrawImage(_logo, logoX, logoY, logoW, logoH)
        End If

        ' ── Titre principal ──
        Dim fTitre = New Font("Segoe UI Light", 28, FontStyle.Regular)
        g.DrawString(TITRE, fTitre, New SolidBrush(COULEUR_TITRE), xTexte, yBase)
        yBase += 46

        ' ── Sous-titre ──
        Dim fSous = New Font("Segoe UI", 11, FontStyle.Regular)
        g.DrawString(SOUS_TITRE, fSous, New SolidBrush(COULEUR_TEXTE), xTexte, yBase)
        yBase += 30

        ' ── Séparateur ──
        Using pen = New Pen(Color.FromArgb(60, 80, 110), 1)
            g.DrawLine(pen, xTexte, yBase, w - 30, yBase)
        End Using
        yBase += 14

        ' ── Nom du labo ──
        Dim fLabo = New Font("Segoe UI", 10, FontStyle.Regular)
        g.DrawString(NOM_LABO, fLabo, New SolidBrush(COULEUR_LABO), xTexte, yBase)
        yBase += 22

        ' ── Nom de l'auteur ──
        If NOM_AUTEUR <> "" Then
            Dim fAuteur = New Font("Segoe UI", 9, FontStyle.Italic)
            g.DrawString(NOM_AUTEUR, fAuteur,
                         New SolidBrush(Color.FromArgb(120, 140, 170)), xTexte, yBase)
            yBase += 22
        End If

        ' ── Version ──
        Dim fVer = New Font("Segoe UI", 9, FontStyle.Regular)
        g.DrawString("Version  " & VERSION, fVer,
                     New SolidBrush(Color.FromArgb(100, 120, 150)), xTexte, yBase)

        ' ── Date de compilation ──
        Dim dateCompil = String.Format("Compilé le {0:dd/MM/yyyy}", _
            File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location))
        Dim szDate = g.MeasureString(dateCompil, fVer)
        g.DrawString(dateCompil, fVer,
                     New SolidBrush(Color.FromArgb(80, 100, 130)),
                     w - szDate.Width - 30, yBase)

        ' ── Message chargement ──
        Dim fMsg = New Font("Segoe UI", 8.5, FontStyle.Italic)
        g.DrawString("Chargement en cours…", fMsg,
                     New SolidBrush(Color.FromArgb(100, 120, 150)),
                     xTexte, h - 48)

        ' ── Barre de progression ──
        Dim barY = h - 28
        Dim barX = 30
        Dim barW = w - 60
        Dim barH = 6

        ' Fond de la barre
        Using br = New SolidBrush(Color.FromArgb(40, 50, 70))
            DrawRoundRect(g, br, barX, barY, barW, barH, 3)
        End Using

        ' Progression
        If _progresseValue > 0 Then
            Dim fillW = CInt(barW * _progresseValue / 100.0)
            If fillW > 6 Then
                Using br = New LinearGradientBrush(
                    New Point(barX, barY), New Point(barX + fillW, barY),
                    Color.FromArgb(60, 130, 220), COULEUR_BARRE)
                    DrawRoundRect(g, br, barX, barY, fillW, barH, 3)
                End Using
            End If
        End If

        ' ── Copyright ──
        Dim fCopy = New Font("Segoe UI", 7.5)
        Dim copyTxt = "© " & DateTime.Now.Year.ToString() & "  —  Cliquer pour fermer"
        Dim szCopy  = g.MeasureString(copyTxt, fCopy)
        g.DrawString(copyTxt, fCopy,
                     New SolidBrush(Color.FromArgb(60, 80, 110)),
                     (w - szCopy.Width) / 2, h - 14)
    End Sub

    ' ─── Rectangle arrondi (helper) ──────────────────────────────────────────

    Private Sub DrawRoundRect(g As Graphics, br As Brush,
                               x As Integer, y As Integer,
                               w As Integer, h As Integer, r As Integer)
        If w <= 0 Then Return
        Using path As New GraphicsPath()
            path.AddArc(x, y, r * 2, r * 2, 180, 90)
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90)
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90)
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90)
            path.CloseFigure()
            g.FillPath(br, path)
        End Using
    End Sub

End Class
