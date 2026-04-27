using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using RestauranteMVP.Core;
using ClosedXML.Excel;

namespace RestauranteMVP.Data
{
    public static class DataStore
    {
        private static readonly string _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestauranteMVP");

        // LISTAS GLOBAIS
        public static List<Product> Products { get; set; } = new();
        public static List<RecipeComponent> Recipes { get; set; } = new();
        public static List<Comanda> Comandas { get; set; } = new();
        public static List<StockMovement> StockMov { get; set; } = new();

        // CONSTRUTOR ESTÁTICO
        static DataStore()
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        // ===== IDs Automáticos =====
        public static int NextProductId() => (Products.Any() ? Products.Max(p => p.Id) : 0) + 1;
        public static int NextComandaId() => (Comandas.Any() ? Comandas.Max(c => c.Id) : 0) + 1;
        public static int NextMovId() => (StockMov.Any() ? StockMov.Max(m => m.Id) : 0) + 1;

        // ===== Persistência (JSON) =====
        public static void Save()
        {
            try
            {
                SaveFile("products.json", Products);
                SaveFile("recipes.json", Recipes);
                SaveFile("comandas.json", Comandas);
                SaveFile("stock_mov.json", StockMov);
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao salvar dados: {ex.Message}"); }
        }

        public static void Load()
        {
            try
            {
                Products = LoadFile<List<Product>>("products.json") ?? new();
                Recipes  = LoadFile<List<RecipeComponent>>("recipes.json") ?? new();
                Comandas = LoadFile<List<Comanda>>("comandas.json") ?? new();
                StockMov = LoadFile<List<StockMovement>>("stock_mov.json") ?? new();

                foreach (var c in Comandas) CalculateTotals(c);
            }
            catch (Exception ex) { MessageBox.Show($"Erro ao carregar dados: {ex.Message}"); }
        }

        private static void SaveFile<T>(string filename, T data)
        {
            var path = Path.Combine(_basePath, filename);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static T? LoadFile<T>(string filename)
        {
            var path = Path.Combine(_basePath, filename);
            if (!File.Exists(path)) return default;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }

        // ===== IMPORTAÇÃO DE EXCEL =====
        public static void LoadFromExcelToMemory(string filePath)
        {
            if (!File.Exists(filePath)) { MessageBox.Show("Arquivo não encontrado."); return; }

            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var ws = workbook.Worksheets.FirstOrDefault();
                    if (ws == null) { MessageBox.Show("Planilha vazia."); return; }

                    var rows = ws.RangeUsed()?.RowsUsed();
                    if (rows == null || rows.Count() < 2)
                    {
                        MessageBox.Show("Planilha sem dados.");
                        return;
                    }

                    int produtosCriados = 0;
                    int receitasCriadas = 0;

                    // 1. PRODUTOS
                    foreach (var row in rows.Skip(1))
                    {
                        var idCell = row.Cell(1).GetValue<string>();
                        var name   = row.Cell(2).GetValue<string>().Trim();
                        var cat    = row.Cell(3).GetValue<string>().Trim();
                        var unit   = row.Cell(4).GetValue<string>().Trim();
                        var price  = row.Cell(5).GetValue<string>();
                        var stock  = row.Cell(6).GetValue<string>();

                        if (string.IsNullOrWhiteSpace(name)) continue;

                        int id = 0;
                        if (int.TryParse(idCell, out int parsedId)) id = parsedId;
                        if (id == 0) id = NextProductId();

                        decimal.TryParse(price, out decimal dPrice);
                        int.TryParse(stock, out int iStock);

                        var existing = Products.FirstOrDefault(p => p.Id == id || p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        
                        if (existing != null)
                        {
                            existing.Name = name;
                            existing.Category = string.IsNullOrWhiteSpace(cat) ? "Outros" : cat;
                            existing.Unit = string.IsNullOrWhiteSpace(unit) ? "un" : unit;
                            existing.Price = dPrice;
                            existing.Stock = iStock;
                        }
                        else
                        {
                            Products.Add(new Product { Id = id, Name = name, Category = string.IsNullOrWhiteSpace(cat) ? "Outros" : cat, Unit = string.IsNullOrWhiteSpace(unit) ? "un" : unit, Price = dPrice, Stock = iStock });
                            produtosCriados++;
                        }
                    }

                    // 2. RECEITAS
                    foreach (var row in rows.Skip(1))
                    {
                        var parentName = row.Cell(2).GetValue<string>().Trim();
                        if (string.IsNullOrEmpty(parentName)) continue;

                        var parentProduct = Products.FirstOrDefault(p => p.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
                        if (parentProduct == null) continue;

                        var receitaString = row.Cell(7).GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(receitaString))
                        {
                            Recipes.RemoveAll(r => r.ParentProductId == parentProduct.Id);
                            var partes = receitaString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (var parte in partes)
                            {
                                var dados = parte.Split('=');
                                if (dados.Length != 2) continue;
                                string nomeIngrediente = dados[0].Trim();
                                string qtdString = dados[1].Trim();

                                var ingrediente = Products.FirstOrDefault(p => p.Name.Equals(nomeIngrediente, StringComparison.OrdinalIgnoreCase));
                                if (ingrediente != null && int.TryParse(qtdString, out int qtd) && qtd > 0)
                                {
                                    Recipes.Add(new RecipeComponent { ParentProductId = parentProduct.Id, IngredientProductId = ingrediente.Id, QuantityPerUnit = qtd });
                                    receitasCriadas++;
                                }
                            }
                        }
                    }
                    MessageBox.Show($"Importação Finalizada!\n📦 Produtos: {Products.Count}\n🔗 Receitas: {receitasCriadas}");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Erro na importação: {ex.Message}"); }
        }

        // ===== Lógica de Negócio =====
        public static Totals CalculateTotals(Comanda c)
        {
            decimal sub = c.Items.Sum(x => x.Price * x.Qty);
            decimal svc = sub * (c.ServicePct / 100m);
            decimal disc = c.DiscountValue;
            if (c.DiscountPct > 0) disc += sub * (c.DiscountPct / 100m);
            decimal total = sub + svc - disc;
            if (total < 0) total = 0;

            decimal paid = 0;
            if (c.Payments != null) paid = c.Payments.Sum(p => p.Amount);
            decimal remaining = total - paid;
            if (remaining < 0) remaining = 0; 

            c.Totals = new Totals { Subtotal = sub, Service = svc, Discounts = disc, Total = total, Paid = paid, Remaining = remaining };
            return c.Totals;
        }

        public static bool TryAddItemToComanda(int comandaId, int productId, int qty, out string error)
        {
            error = "";
            var c = Comandas.FirstOrDefault(x => x.Id == comandaId);
            if (c == null) { error = "Comanda não encontrada."; return false; }
            var p = Products.FirstOrDefault(x => x.Id == productId);
            if (p == null) { error = "Produto não encontrado."; return false; }

            var recipe = Recipes.Where(r => r.ParentProductId == p.Id).ToList();
            if (recipe.Any())
            {
                foreach (var item in recipe)
                {
                    var ing = Products.FirstOrDefault(x => x.Id == item.IngredientProductId);
                    if (ing == null) continue;
                    if (ing.Stock < item.QuantityPerUnit * qty) { error = $"Estoque insuficiente: {ing.Name}"; return false; }
                }
                foreach (var item in recipe)
                {
                    var ing = Products.First(x => x.Id == item.IngredientProductId);
                    ing.Stock -= (item.QuantityPerUnit * qty);
                    StockMov.Add(new StockMovement { Id = NextMovId(), ProductId = ing.Id, Qty = item.QuantityPerUnit * qty, Type = "out", ComandaId = c.Id, Timestamp = DateTime.Now });
                }
            }
            else
            {
                if (p.Category != "Ingredientes" && p.Stock < qty) { error = $"Estoque insuficiente: {p.Name}"; return false; }
                p.Stock -= qty;
                StockMov.Add(new StockMovement { Id = NextMovId(), ProductId = p.Id, Qty = qty, Type = "out", ComandaId = c.Id, Timestamp = DateTime.Now });
            }

            var existingItem = c.Items.FirstOrDefault(i => i.ProductId == p.Id);
            if (existingItem != null) existingItem.Qty += qty;
            else c.Items.Add(new ComandaItem { ProductId = p.Id, Name = p.Name, Price = p.Price, Qty = qty });

            CalculateTotals(c); Save(); return true;
        }

        public static bool TryRemoveItemFromComanda(int comandaId, int productId, int qtyToRemove, out string error)
        {
            error = "";
            var c = Comandas.FirstOrDefault(x => x.Id == comandaId);
            if (c == null) return false;
            var item = c.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return false;
            if (qtyToRemove > item.Qty) qtyToRemove = item.Qty;

            var p = Products.FirstOrDefault(x => x.Id == productId);
            if (p != null)
            {
                var recipe = Recipes.Where(r => r.ParentProductId == p.Id).ToList();
                if (recipe.Any())
                {
                    foreach (var comp in recipe)
                    {
                        var ing = Products.FirstOrDefault(i => i.Id == comp.IngredientProductId);
                        if (ing != null) {
                            int returnQty = comp.QuantityPerUnit * qtyToRemove;
                            ing.Stock += returnQty;
                            StockMov.Add(new StockMovement { Id = NextMovId(), ProductId = ing.Id, Qty = returnQty, Type = "in", ComandaId = c.Id, Timestamp = DateTime.Now });
                        }
                    }
                }
                else
                {
                    p.Stock += qtyToRemove;
                    StockMov.Add(new StockMovement { Id = NextMovId(), ProductId = p.Id, Qty = qtyToRemove, Type = "in", ComandaId = c.Id, Timestamp = DateTime.Now });
                }
            }
            item.Qty -= qtyToRemove;
            if (item.Qty <= 0) c.Items.Remove(item);
            CalculateTotals(c); Save(); return true;
        }

        public static bool CloseComandaSafe(int comandaId, out string error)
        {
            error = "";
            var c = Comandas.FirstOrDefault(x => x.Id == comandaId);
            if (c == null) return false;
            CalculateTotals(c);
            if (c.Totals.Remaining > 0.05m) { error = $"Falta pagar {c.Totals.Remaining:C}."; return false; }
            c.Status = "closed"; c.ClosedAt = DateTime.Now; Save(); return true;
        }

        public static bool CancelComanda(int comandaId, out string error)
        {
            error = "";
            var c = Comandas.FirstOrDefault(x => x.Id == comandaId);
            if (c == null) return false;
            foreach (var item in c.Items.ToList()) TryRemoveItemFromComanda(c.Id, item.ProductId, item.Qty, out _);
            c.Status = "canceled"; c.ClosedAt = DateTime.Now; Save(); return true;
        }
    }
}