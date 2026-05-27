''' <summary>
''' Informations de version centralisées.
''' Pour mettre à jour la version : modifier uniquement VERSION ici.
''' Tous les affichages (titre fenêtre, splash, barre de statut) utilisent ces constantes.
''' </summary>
Public Module AppInfo

    ''' <summary>Version courante — ex: "v2.1b"</summary>
    Public Const VERSION As String = "v2.2"

    ''' <summary>Nom complet affiché dans la barre de titre et le splash.</summary>
    Public ReadOnly Property TitreComplet As String
        Get
            Return "Thermopilott " & VERSION & " — Acquisition Multi-Centrale (IRDL PTR4)"
        End Get
    End Property

    ''' <summary>Libellé court pour la barre de statut.</summary>
    Public ReadOnly Property TitreCourt As String
        Get
            Return "Thermopilot " & VERSION & " — Adrien Fuentes"
        End Get
    End Property

End Module
