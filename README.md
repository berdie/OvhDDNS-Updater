# OvhDDNS-Updater 
#### Ovh DynHost Ip Updater
Il client aggiorna i record di tipo A
Al primo avvio crea un file di configurazione nella cartella di installazione (default: 'C:\Program Files (x86)\OvhDDNS\OvhDDNS-Updater-Client') 'config.json'
#### Richiede:
- **apiBaseUrl**: (EU="https://eu.api.ovh.com/1.0/domain/zone/")
- **applicationKey**:
- **applicationSecret**:
- **consumerKey**:

#### Passaggi per ottenere le API Key:
Vai su
https://eu.api.ovh.com/createToken/
e, dopo aver effettuato di il login, imposta:
- Script Name: App Name es. 'OvhDDNS-Updater'
- Description: 'App Description'
- Validity: ad esempio “Unlimited”
usa il pulsante “+” per aggiungere le regole di accesso (GET, POST, PUT, DELETE) sui path di tuo interesse, p.es. /domain/zone/*
Al click su Create Keys, la pagina ti restituirà insieme ad appKey/appSecret anche la Consumer Key.
