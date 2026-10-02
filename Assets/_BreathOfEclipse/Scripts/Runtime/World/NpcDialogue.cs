using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Short contextual lines (Spanish, like the game's voices): greetings by time of day, a few lines per trade and
    /// person, reactions to danger, injuries and to what happened in the world (facts). Talking again cycles lines.
    /// </summary>
    public static class NpcDialogue
    {
        private static readonly Dictionary<string, int> Counters = new Dictionary<string, int>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Counters.Clear();

        private static readonly Dictionary<NpcRole, string[]> RoleLines = new Dictionary<NpcRole, string[]>
        {
            { NpcRole.Farmer, new[] { "Este año el arroz viene bien... si la lluvia ayuda.", "Los jabalíes otra vez se comieron los nabos.", "La tierra no espera. Hay que sembrar antes de que cambie el viento." } },
            { NpcRole.Merchant, new[] { "¡Arroz, sal, hierbas medicinales! Mira sin compromiso.", "Las caravanas llegan tarde últimamente. Todo sube de precio.", "Si vas al bosque, lleva hierbas. Nunca se sabe." } },
            { NpcRole.Smith, new[] { "Una buena hoja necesita paciencia. Y buen carbón.", "Si tu espada se mella, tráemela. No cobro a los que cuidan la aldea.", "El acero de los cazadores es distinto... pero no me preguntes cómo lo forjan." } },
            { NpcRole.Lumberjack, new[] { "El roble del este está duro como piedra.", "Cuidado junto al estanque, el suelo resbala.", "Cortamos solo lo necesario. El bosque se cobra lo que le quitas." } },
            { NpcRole.Fisher, new[] { "Shh... que se espantan los peces.", "Junto a la cascada pican más, pero ya nadie va solo.", "El río estuvo raro anoche. Ni una rana cantaba." } },
            { NpcRole.Parent, new[] { "¡No corran tan lejos!", "Con este viento la ropa se seca en nada.", "Los niños preguntan por los cazadores todo el día." } },
            { NpcRole.Child, new[] { "¡No me atrapas!", "¿Eres cazador de verdad? ¿Me enseñas tu espada?", "Mamá dice que de noche hay que estar adentro. ¿Por qué?" } },
            { NpcRole.Guard, new[] { "La puerta norte se vigila toda la noche.", "Si suena la campana, todos a la posada. Sin preguntas.", "Anoche vimos ojos rojos entre los árboles. No se acercaron." } },
            { NpcRole.Traveler, new[] { "Vengo de lejos. El camino del norte es largo y oscuro.", "En el campamento se duerme mejor con el fuego alto.", "Dicen que más allá de la puerta rota nadie vuelve igual." } },
            { NpcRole.Priest, new[] { "Que la luz de la luna te acompañe.", "Los guardianes de piedra del bosque profundo aún vigilan. Alguien les lleva flores.", "Respira hondo. El miedo se va con el aire." } },
            { NpcRole.Villager, new[] { "Buen día. ¿Vienes de paso?", "La posada tiene futones limpios, si necesitas descansar.", "Desde que se fue el último cazador, dormimos con un ojo abierto." } },
            { NpcRole.Hunter, new[] { "Mantente en el camino. Fuera de él, el bosque es suyo.", "Si ves marcas de garras en los árboles, da la vuelta.", "Las bestias huyen de la luz. Las grandes, no." } },
            { NpcRole.Camper, new[] { "El río habla, si sabes escuchar.", "Dicen que los demonios no cruzan el agua corriente. Yo no apostaría.", "Siéntate un rato. El fuego no pregunta de dónde vienes." } },
        };

        private static readonly Dictionary<string, string[]> PersonLines = new Dictionary<string, string[]>
        {
            { "toku", new[] { "En mis tiempos, la Hondonada de Ceniza era un bosque como cualquier otro.", "No te acerques al noreste. El aire allí está mal.", "Mi esposa plantó ese cerezo. Florece cada año, como si nada." } },
            { "sayo", new[] { "Bienvenido a la posada Kasumi. Hay té caliente.", "¿Necesitas descansar? El futón del fondo es el más tranquilo.", "Los viajeros cuentan historias raras últimamente." } },
            { "shion", new[] { "Que la luz de la luna te acompañe, joven.", "Cada mañana rezo por los que están en el camino.", "Hay lugares en este bosque que no figuran en ningún mapa. Mejor así." } },
            { "kei", new[] { "¡Mio siempre gana a las escondidas!", "¡Quiero ser cazador cuando sea grande!", "¿Viste el árbol gigante? ¡Es más alto que la torre!" } },
            { "mio", new[] { "¡Kei hace trampa!", "Encontré una flor azul junto al pozo.", "¿Tu espada pesa mucho?" } },
            { "jubei", new[] { "Puerta norte. Nadie pasa de noche sin linterna.", "Si suena la campana, a la posada. Repito: a la posada.", "No soy cazador. Solo un hombre con una lanza y mucho miedo." } },
            { "ume", new[] { "Telas de la capital, baratas. Bueno... casi baratas.", "Vendo, compro, escucho rumores. Todo tiene su precio.", "Una caravana no llegó la semana pasada. Nadie habla de eso." } },
        };

        public static string Greeting(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Dawn: return "Madrugaste. El rocío todavía no se seca.";
                case DayPhase.Morning: return "Buenos días.";
                case DayPhase.Day: return "Buen día para trabajar.";
                case DayPhase.Afternoon: return "Buenas tardes.";
                case DayPhase.Sunset: return "Pronto oscurecerá. No te alejes de la aldea.";
                default: return "¿Qué haces afuera a estas horas?";
            }
        }

        /// <summary>The line said when the player talks to <paramref name="n"/>.</summary>
        public static string Talk(NpcSimState n, DayPhase phase, WorldStateDatabase world, bool shopOpen)
        {
            if (n.Injured) return "Ayuda... por favor...";
            if (n.Mode == NpcMode.Fleeing || n.Mode == NpcMode.Afraid) return "¡Un demonio! ¡Corre a la posada!";
            string id = n.Def.Id;
            Counters.TryGetValue(id, out int count);
            Counters[id] = count + 1;

            // What happened in the world comes first (once each).
            if (world != null)
            {
                string fact = WorldReaction(world, id);
                if (fact != null) return fact;
            }
            if (n.Def.Role == NpcRole.Merchant && !shopOpen) return "Ya cerré por hoy. Vuelve mañana temprano.";
            bool night = phase == DayPhase.Night || phase == DayPhase.LateNight;
            if (night && n.Def.Role != NpcRole.Guard && n.Def.Role != NpcRole.Hunter && n.Def.Role != NpcRole.Camper && n.Def.Role != NpcRole.Traveler)
                return "Es de noche. Deberías estar bajo techo.";
            if (count == 0) return Greeting(phase);
            string[] lines = PersonLines.TryGetValue(id, out var own) ? own : RoleLines.TryGetValue(n.Def.Role, out var role) ? role : RoleLines[NpcRole.Villager];
            return lines[(count - 1) % lines.Length];
        }

        /// <summary>Something said aloud nearby (no player prompt).</summary>
        public static string Bark(NpcSimState n, DayPhase phase, int salt)
        {
            if (n.Mode == NpcMode.Fleeing) return salt % 2 == 0 ? "¡Corran! ¡A las casas!" : "¡Rápido, a la posada!";
            if (n.Mode == NpcMode.Afraid) return "No... no...";
            if (n.Injured) return "Alguien... ayuda...";
            switch (n.Activity)
            {
                case NpcActivity.Trade: return salt % 3 == 0 ? "¡Arroz fresco! ¡Hierbas para el camino!" : salt % 3 == 1 ? "¡Pasen, pasen!" : "¡Sal de la costa, recién llegada!";
                case NpcActivity.Play: return salt % 2 == 0 ? "¡Te atrapé!" : "¡No vale, no vale!";
                case NpcActivity.Guard: return phase == DayPhase.Night || phase == DayPhase.LateNight ? "...Silencio. Demasiado silencio." : "Todo tranquilo por ahora.";
                case NpcActivity.Farm: return "Uf... la espalda.";
                case NpcActivity.Chop: return "¡Cuidado, que cae!";
                case NpcActivity.Talk: return salt % 2 == 0 ? "¿Te enteraste de lo de la caravana?" : "Dicen que viene un invierno duro.";
                case NpcActivity.Smith: return "Un golpe más...";
                case NpcActivity.Pray: return "Que la luna nos guarde.";
            }
            if (phase == DayPhase.Sunset) return salt % 2 == 0 ? "Hora de volver a casa." : "Ya oscurece. ¡Adentro, niños!";
            return null;
        }

        public static string Thanks() => "Gracias... no lo olvidaré.";

        private static string WorldReaction(WorldStateDatabase world, string npcId)
        {
            // Each reaction is told once per NPC.
            string Once(string fact, string line)
            {
                if (world.GetFact(fact) <= 0) return null;
                string told = $"told_{npcId}_{fact}";
                if (world.GetFact(told) > 0) return null;
                world.SetFact(told, 1, 0);
                return line;
            }
            return Once("Saved_Caravan_001", "Dicen que salvaste a la caravana. La aldea no lo olvida.")
                   ?? Once("Lost_Caravan_001", "La caravana no llegó... Encontraron el carro destrozado en el camino.")
                   ?? Once("Saved_Family_001", "Esa familia del camino está viva gracias a ti.")
                   ?? Once("Village_Attacked", "La cerca del este quedó destrozada. Estamos reparándola.")
                   ?? Once("Found_Child_001", "Gracias por traer al niño de vuelta. Su madre ya está más tranquila.")
                   ?? Once("Hunter_Saved", "Ese cazador herido... dicen que lo trajiste tú. Hay gente buena todavía.");
        }
    }
}
