using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OvhDDNS_Updater
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                // Caricare la configurazione
                var config = LoadConfiguration();
                if (config == null)
                {
                    Console.WriteLine("File di configurazione non trovato o non valido. Creazione del file di esempio...");
                    CreateSampleConfigFile();
                    Console.WriteLine("File di configurazione di esempio creato. Modificarlo con i propri dati e riavviare l'applicazione.");
                    Console.WriteLine("Premere un tasto per uscire...");
                    Console.ReadKey();
                    return;
                }

                // Step 1: Ottenere l'indirizzo IP pubblico
                string publicIp = await GetPublicIpAsync();
                Console.WriteLine($"Indirizzo IP pubblico: {publicIp}");

                // Step 2: Aggiornare il record DNS
                foreach (var dnsRecord in config.DnsRecords)
                {
                    await UpdateDnsRecordAsync(config.OvhCredentials, dnsRecord.Domain, dnsRecord.Subdomain, publicIp);
                    Console.WriteLine($"Record DNS {dnsRecord.Subdomain}.{dnsRecord.Domain} aggiornato con successo!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
            }

            Console.WriteLine("Premere un tasto per uscire...");
            Console.ReadKey();
        }

        private static async Task<string> GetPublicIpAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Prova con il primo servizio
                    HttpResponseMessage response = await client.GetAsync("https://api.ipify.org");
                    response.EnsureSuccessStatusCode();
                    return (await response.Content.ReadAsStringAsync()).Trim();
                }
                catch
                {
                    // Se il primo servizio fallisce, prova con il secondo
                    HttpResponseMessage response = await client.GetAsync("https://ipv4.icanhazip.com/");
                    response.EnsureSuccessStatusCode();
                    return (await response.Content.ReadAsStringAsync()).Trim();
                }
            }
        }

        private static async Task UpdateDnsRecordAsync(OvhCredentials credentials, string domain, string subdomain, string ip)
        {
            // 1. Prima, ottieni l'ID del record DNS
            long recordId = await GetDnsRecordIdAsync(credentials, domain, subdomain);

            if (recordId <= 0)
            {
                throw new Exception($"Record DNS per {subdomain}.{domain} non trovato");
            }

            Console.WriteLine($"Aggiornamento record con ID: {recordId}");

            // 2. Aggiorna il record DNS con il nuovo IP
            // Correggiamo l'URL per puntare al record specifico che vogliamo aggiornare
            string apiUrl = $"{LoadConfiguration().ApiBaseUrl}{domain}/record/{recordId}";
            string method = "PUT";
            string body = $"{{\"target\":\"{ip}\"}}";
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            using (HttpClient client = new HttpClient())
            {
                // Imposta le intestazioni di autenticazione
                client.DefaultRequestHeaders.Add("X-Ovh-Application", credentials.ApplicationKey);
                client.DefaultRequestHeaders.Add("X-Ovh-Timestamp", timestamp.ToString());
                client.DefaultRequestHeaders.Add("X-Ovh-Consumer", credentials.ConsumerKey);

                // Genera la firma HMAC
                string signature = GenerateOvhSignature(credentials.ApplicationSecret, credentials.ConsumerKey,
                    method, apiUrl, body, timestamp);
                client.DefaultRequestHeaders.Add("X-Ovh-Signature", signature);

                // Esegui la richiesta PUT
                var content = new StringContent(body, Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await client.PutAsync(apiUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Errore API ({response.StatusCode}): {errorContent}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Errore di connessione API: {ex.Message}");
                }
            }

            // 3. Applica le modifiche alla zona DNS
            await RefreshDnsZoneAsync(credentials, domain);
        }


        // Modifica anche il metodo GetDnsRecordIdAsync per restituire long invece di int
        private static async Task<long> GetDnsRecordIdAsync(OvhCredentials credentials, string domain, string subdomain)
        {
            string apiUrl = $"{LoadConfiguration().ApiBaseUrl}{domain}/record?fieldType=A&subDomain={subdomain}";
            string method = "GET";
            string body = string.Empty;
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            using (HttpClient client = new HttpClient())
            {
                // Imposta le intestazioni di autenticazione
                client.DefaultRequestHeaders.Add("X-Ovh-Application", credentials.ApplicationKey);
                client.DefaultRequestHeaders.Add("X-Ovh-Timestamp", timestamp.ToString());
                client.DefaultRequestHeaders.Add("X-Ovh-Consumer", credentials.ConsumerKey);

                // Genera la firma HMAC
                string signature = GenerateOvhSignature(credentials.ApplicationSecret, credentials.ConsumerKey,
                    method, apiUrl, body, timestamp);
                client.DefaultRequestHeaders.Add("X-Ovh-Signature", signature);

                // Esegui la richiesta GET
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Risposta API per il record DNS: {content}");

                try
                {
                    long[] recordIds = JsonSerializer.Deserialize<long[]>(content);
                    if (recordIds != null && recordIds.Length > 0)
                    {
                        return recordIds[0];
                    }

                    Console.WriteLine("Nessun record trovato nell'array.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore durante il parsing JSON: {ex.Message}");
                    Console.WriteLine($"JSON ricevuto: {content}");
                    return 0;
                }
            }
        }





        private static async Task RefreshDnsZoneAsync(OvhCredentials credentials, string domain)
        {
            string apiUrl = $"https://eu.api.ovh.com/1.0/domain/zone/{domain}/refresh";
            string method = "POST";
            string body = string.Empty;
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            using (HttpClient client = new HttpClient())
            {
                // Imposta le intestazioni di autenticazione
                client.DefaultRequestHeaders.Add("X-Ovh-Application", credentials.ApplicationKey);
                client.DefaultRequestHeaders.Add("X-Ovh-Timestamp", timestamp.ToString());
                client.DefaultRequestHeaders.Add("X-Ovh-Consumer", credentials.ConsumerKey);

                // Genera la firma HMAC
                string signature = GenerateOvhSignature(credentials.ApplicationSecret, credentials.ConsumerKey,
                    method, apiUrl, body, timestamp);
                client.DefaultRequestHeaders.Add("X-Ovh-Signature", signature);

                // Esegui la richiesta POST
                var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                response.EnsureSuccessStatusCode();
            }
        }

        private static string GenerateOvhSignature(string applicationSecret, string consumerKey,
            string method, string url, string body, long timestamp)
        {
            // Creazione della stringa da firmare secondo la documentazione dell'API OVH
            StringBuilder signatureBuilder = new StringBuilder();
            signatureBuilder.Append(applicationSecret);
            signatureBuilder.Append("+");
            signatureBuilder.Append(consumerKey);
            signatureBuilder.Append("+");
            signatureBuilder.Append(method);
            signatureBuilder.Append("+");
            signatureBuilder.Append(url);
            signatureBuilder.Append("+");
            signatureBuilder.Append(body);
            signatureBuilder.Append("+");
            signatureBuilder.Append(timestamp);

            // Calcolo dell'hash SHA-1
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(signatureBuilder.ToString());
                byte[] hash = sha1.ComputeHash(bytes);
                return "$1$" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static Config LoadConfiguration()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            if (!File.Exists(configPath))
                return null;

            string json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static void CreateSampleConfigFile()
        {
            Config sampleConfig = new Config
            {
                ApiBaseUrl = "https://eu.api.ovh.com/1.0/domain/zone/", // URL base di esempio
                OvhCredentials = new OvhCredentials
                {
                    ApplicationKey = "YOUR_APPLICATION_KEY",
                    ApplicationSecret = "YOUR_APPLICATION_SECRET",
                    ConsumerKey = "YOUR_CONSUMER_KEY"
                },
                DnsRecords = new[]
                {
            new DnsRecord
            {
                Domain = "example.com",
                Subdomain = "www"
            },
            new DnsRecord
            {
                Domain = "example.com",
                Subdomain = "mail"
            }
        }
            };

            string json = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            File.WriteAllText(configPath, json);
        }

    }

    public class Config
    {
        public string ApiBaseUrl { get; set; } // Nuova proprietà per l'URL base
        public OvhCredentials OvhCredentials { get; set; }
        public DnsRecord[] DnsRecords { get; set; }
    }


    public class OvhCredentials
    {
        public string ApplicationKey { get; set; }
        public string ApplicationSecret { get; set; }
        public string ConsumerKey { get; set; }
    }

    public class DnsRecord
    {
        public string Domain { get; set; }
        public string Subdomain { get; set; }
    }
}
