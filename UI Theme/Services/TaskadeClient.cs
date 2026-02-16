using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ProjectTactics.Models;
using RestSharp;

namespace ProjectTactics.Services
{
    /// <summary>
    /// Client for communicating with Taskade API
    /// </summary>
    public class TaskadeClient
    {
        private const string API_BASE_URL = "https://www.taskade.com/api/taskade";
        private const string CHARACTER_PROJECT_ID = "5TWDhiiEpuZBi72G"; // Replace with your project ID
        
        private readonly RestClient _client;
        
        public TaskadeClient()
        {
            _client = new RestClient(API_BASE_URL);
        }
        
        /// <summary>
        /// Fetch character data from Taskade project
        /// </summary>
        public async Task<Character?> GetCharacterAsync()
        {
            try
            {
                var request = new RestRequest($"/projects/{CHARACTER_PROJECT_ID}/nodes");
                var response = await _client.ExecuteAsync(request);
                
                if (!response.IsSuccessful || response.Content == null)
                {
                    return null;
                }
                
                var json = JObject.Parse(response.Content);
                var nodes = json["payload"]?["nodes"] as JArray;
                
                if (nodes == null || nodes.Count == 0)
                {
                    return null;
                }
                
                // Parse character from first node
                var characterNode = nodes[0];
                
                return new Character
                {
                    Id = characterNode["id"]?.ToString() ?? string.Empty,
                    FullName = GetFieldValue(characterNode, "Full Name"),
                    Race = GetFieldValue(characterNode, "Race"),
                    City = GetFieldValue(characterNode, "City"),
                    RankTitle = GetFieldValue(characterNode, "Rank/Title"),
                    Bio = GetFieldValue(characterNode, "Bio"),
                    PortraitUrl = GetFieldValue(characterNode, "Portrait"),
                    Strength = GetStatValue(characterNode, "STR"),
                    Speed = GetStatValue(characterNode, "SPD"),
                    Agility = GetStatValue(characterNode, "AGI"),
                    Endurance = GetStatValue(characterNode, "END"),
                    Stamina = GetStatValue(characterNode, "STA"),
                    Ether = GetStatValue(characterNode, "ETH")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching character: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Fetch all characters from the project
        /// </summary>
        public async Task<List<Character>> GetAllCharactersAsync()
        {
            try
            {
                var request = new RestRequest($"/projects/{CHARACTER_PROJECT_ID}/nodes");
                var response = await _client.ExecuteAsync(request);
                
                if (!response.IsSuccessful || response.Content == null)
                {
                    return new List<Character>();
                }
                
                var json = JObject.Parse(response.Content);
                var nodes = json["payload"]?["nodes"] as JArray;
                
                if (nodes == null)
                {
                    return new List<Character>();
                }
                
                var characters = new List<Character>();
                
                foreach (var node in nodes)
                {
                    characters.Add(new Character
                    {
                        Id = node["id"]?.ToString() ?? string.Empty,
                        FullName = GetFieldValue(node, "Full Name"),
                        Race = GetFieldValue(node, "Race"),
                        City = GetFieldValue(node, "City"),
                        RankTitle = GetFieldValue(node, "Rank/Title"),
                        Bio = GetFieldValue(node, "Bio"),
                        PortraitUrl = GetFieldValue(node, "Portrait"),
                        Strength = GetStatValue(node, "STR"),
                        Speed = GetStatValue(node, "SPD"),
                        Agility = GetStatValue(node, "AGI"),
                        Endurance = GetStatValue(node, "END"),
                        Stamina = GetStatValue(node, "STA"),
                        Ether = GetStatValue(node, "ETH")
                    });
                }
                
                return characters;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching characters: {ex.Message}");
                return new List<Character>();
            }
        }
        
        /// <summary>
        /// Helper method to extract field value from node
        /// </summary>
        private string GetFieldValue(JToken node, string fieldName)
        {
            var fields = node["fields"] as JObject;
            if (fields == null) return string.Empty;
            
            foreach (var field in fields)
            {
                var fieldData = field.Value as JObject;
                if (fieldData?["name"]?.ToString() == fieldName)
                {
                    return fieldData["value"]?.ToString() ?? string.Empty;
                }
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Helper method to extract numeric stat value
        /// </summary>
        private int GetStatValue(JToken node, string statName)
        {
            var valueStr = GetFieldValue(node, statName);
            return int.TryParse(valueStr, out int result) ? result : 0;
        }
    }
}
