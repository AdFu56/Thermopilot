Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Fenêtre contextuelle de saisie d'une notification utilisateur.
''' La notification est horodatée automatiquement et enregistrée dans le CSV.
''' </summary>
Public Class FormNotification
    Inherits Form

    Public ReadOnly Property TexteNotification As String
        Get
            Return _txtMessage.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property Horodatage As DateTime = DateTime.Now

    Private _txtMessage As New RichTextBox()
    Private _btnValider  As New Button()
    Private _btnAnnuler  As New Button()
    Private _lblHeure    As New Label()

    Public Sub New()
        Me.Text            = "📌 Notification"
        Me.Size            = New Size(500, 260)
        Me.StartPosition   = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox     = False
        Me.MinimizeBox     = False
        Me.BackColor       = Color.White

        Dim lblTitre As New Label() With {
            .Text      = "Saisir une notification à enregistrer avec l'horodatage courant :",
            .Dock      = DockStyle.Top,
            .Height    = 28,
            .Font      = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 60, 120),
            .Padding   = New Padding(10, 8, 0, 0)
        }

        _lblHeure.Text      = "Horodatage : " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        _lblHeure.Dock      = DockStyle.Top
        _lblHeure.Height    = 22
        _lblHeure.Font      = New Font("Segoe UI", 8.5, FontStyle.Italic)
        _lblHeure.ForeColor = Color.Gray
        _lblHeure.Padding   = New Padding(10, 2, 0, 0)

        _txtMessage.Dock        = DockStyle.Fill
        _txtMessage.Font        = New Font("Segoe UI", 10)
        _txtMessage.BorderStyle = BorderStyle.FixedSingle
        _txtMessage.ScrollBars  = RichTextBoxScrollBars.Vertical
        _txtMessage.Margin      = New Padding(10)

        Dim pnlBas As New FlowLayoutPanel() With {
            .Dock         = DockStyle.Bottom,
            .Height       = 44,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding      = New Padding(8, 6, 8, 6),
            .BackColor    = Color.FromArgb(245, 247, 252)
        }

        _btnAnnuler.Text         = "Annuler"
        _btnAnnuler.Width        = 90
        _btnAnnuler.Height       = 30
        _btnAnnuler.DialogResult = DialogResult.Cancel
        _btnAnnuler.FlatStyle    = FlatStyle.Flat
        _btnAnnuler.BackColor    = Color.FromArgb(220, 222, 230)

        _btnValider.Text         = "✔ Enregistrer"
        _btnValider.Width        = 120
        _btnValider.Height       = 30
        _btnValider.DialogResult = DialogResult.OK
        _btnValider.FlatStyle    = FlatStyle.Flat
        _btnValider.BackColor    = Color.FromArgb(40, 110, 175)
        _btnValider.ForeColor    = Color.White
        _btnValider.Font         = New Font("Segoe UI", 9, FontStyle.Bold)
        _btnValider.Margin       = New Padding(6, 0, 0, 0)

        pnlBas.Controls.AddRange({_btnAnnuler, _btnValider})

        Me.AcceptButton = _btnValider
        Me.CancelButton = _btnAnnuler

        Me.Controls.Add(_txtMessage)
        Me.Controls.Add(_lblHeure)
        Me.Controls.Add(lblTitre)
        Me.Controls.Add(pnlBas)

        ' Fixer l'horodatage à l'ouverture (pas au moment de valider)
        Horodatage = DateTime.Now
        _lblHeure.Text = "Horodatage : " & Horodatage.ToString("yyyy-MM-dd HH:mm:ss")
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _txtMessage.Focus()
    End Sub

End Class
