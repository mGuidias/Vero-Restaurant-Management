using System;
using System.Collections.Generic;

namespace RestauranteMVP.Core
{
    public class Payment
    {
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Dinheiro"; 
        public DateTime Timestamp { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Outros";
        public string Unit { get; set; } = "un";
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    public class RecipeComponent
    {
        public int ParentProductId { get; set; }      
        public int IngredientProductId { get; set; }  
        public int QuantityPerUnit { get; set; }      
    }

    public class ComandaItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Qty { get; set; }

        // --- NOVOS CAMPOS PARA SALVAR A DIVISÃO ---
        public int SplitTotalParts { get; set; } = 0; // Em quantas vezes foi dividido
        public int SplitPaidParts { get; set; } = 0;  // Quantas já foram pagas
        public int QtyPaid { get; set; } = 0;
        // ------------------------------------------
    }
    public class Comanda
    {
        public int Id { get; set; }
        public string TableName { get; set; } = "";
        public string Status { get; set; } = "open"; 
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public List<ComandaItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new(); 

        public decimal ServicePct { get; set; }
        
        // CORRIGIDO: Agora se chama DiscountValue (antes era DiscountVal)
        public decimal DiscountValue { get; set; } 
        
        public decimal DiscountPct { get; set; }
        public Totals Totals { get; set; }
    }

    public struct Totals
    {
        public decimal Subtotal;
        public decimal Service;
        public decimal Discounts;
        public decimal Total;
        public decimal Paid;      
        public decimal Remaining; 
    }

    public class StockMovement
    {
        public int Id { get; set; }
        public string Type { get; set; } = "out"; 
        public int ProductId { get; set; }
        public int Qty { get; set; }
        public DateTime Timestamp { get; set; }
        public int? ComandaId { get; set; }
    }
}