Imports System.Windows.Forms
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Drawing
Imports System.Linq

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Afficher le splash et attendre qu'il se ferme de lui-même (timer interne)
        ' Le formulaire principal est créé pendant ce temps
        Dim principal As FormPrincipal = Nothing
        Dim splash As New FormSplash()

        ' Créer le formulaire principal une fois le splash fermé
        AddHandler splash.FormClosed, Sub(s, e)
            principal = New FormPrincipal()
        End Sub

        ' Le splash se ferme seul après DUREE_MS ou sur clic
        Application.Run(splash)

        ' Démarrer l'application principale
        If principal IsNot Nothing Then
            Application.Run(principal)
        End If
    End Sub
End Module
