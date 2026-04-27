#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic; // Necessário para InputBox
using RestauranteMVP.Core;
using RestauranteMVP.Data;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace RestauranteMVP.UI
{
    public class MainForm : Form
    {
        // Painéis de Layout
        private Panel leftPanel = new();
        private Panel centerPanel = new();
        private Panel rightPanel = new();

        // Painel Esquerdo (Catálogo Rápido)
        private TextBox searchBox = new();
        private Button addProductBtn = new();
        private Button addIngredientBtn = new();
        private FlowLayoutPanel productList = new();
        private ToolTip stockTooltip = new ToolTip();

        // Painel Central (Comanda Ativa)
        private ComboBox comandaSelect = new();
        private TextBox tableInput = new();
        private Button newComandaBtn = new Button();
        private Button btnAddItems = new Button(); 
        private Label comandaInfo = new();
        private DataGridView itemsGrid = new();

        // Painel Direito (Fechamento)
        private NumericUpDown servicePct = new();
        private NumericUpDown discountVal = new();
        private NumericUpDown discountPct = new();
        private Label subtotalLbl = new();
        private Label serviceLbl = new();
        private Label discountsLbl = new();
        private Label totalLbl = new();
        private Button printBtn = new();
        private Button cancelBtn = new();
        private Button closeBtn = new();
        
        // Header (Topo)
        private Button restockBtn = new();
        private Button exportBtn = new();
        private Button importBtn = new();
        private Button resetBtn = new();
        private Button historyBtn = new();

        // Widget de Estoque Baixo
        private GroupBox lowStockGroup = new();
        private DataGridView lowStockGrid = new();

        // Impressão
        private PrintDocument comandaPrintDocument = new PrintDocument();
        private PrintPreviewDialog printPreviewDialog = new PrintPreviewDialog();

        // Campos auxiliares
        //private ContextMenuStrip? printMenu; 
        //private ToolStripMenuItem? itemVisualizar;
        //private ToolStripMenuItem? itemImprimir;

        // Variáveis de Controle
        private int? currentComandaId = null;
        private const int LOW_STOCK_THRESHOLD = 5;

        public MainForm()
        {
            Text = "🍽️ VERO ";
            //WindowState = FormWindowState.Maximized; 
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            comandaPrintDocument.PrintPage += ComandaPrintDocument_PrintPage;
            printPreviewDialog.Document = comandaPrintDocument;

            ApplyDarkTheme();
            BuildLayout();

            // --- ESTA LINHA É FUNDAMENTAL ---
            // Ela garante que os dados sejam carregados ao abrir
            Init();

            this.Shown += (s, e) => {
                RefreshProducts(); 
                RefreshComandasSelect();
            };
        }

        private void ApplyDarkTheme()
        {
            var bg = Color.FromArgb(0x0f, 0x17, 0x2a);
            var card = Color.FromArgb(0x1f, 0x29, 0x37);
            BackColor = bg;
            ForeColor = Color.Gainsboro;

            void StylePanel(Panel p)
            {
                p.BackColor = card;
                p.Padding = new Padding(10);
                p.Dock = DockStyle.Fill;
            }
            StylePanel(leftPanel);
            StylePanel(centerPanel);
            StylePanel(rightPanel);
        }

        private Button MkBtn(string text, EventHandler? onClick = null, Color? bg = null, Color? fg = null)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = fg ?? Color.Black,
                BackColor = bg ?? Color.FromArgb(0x22, 0xc5, 0x5e),
                Padding = new Padding(6),
                Height = 32,
                AutoSize = true,
                Margin = new Padding(6, 6, 0, 6)
            };
            b.FlatAppearance.BorderSize = 0;
            if (onClick != null) b.Click += onClick;
            return b;
        }

        private Label MkTitle(string text) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.FromArgb(31, 41, 55),
            AutoSize = false,
            Height = 32,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        private void BuildLayout()
        {
            // === HEADER (TOPO) ===
            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(0x11, 0x18, 0x27) };
            var title = new Label
            {
                Text = "🍽️ VERO ",
                AutoSize = false,
                Width = 400,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gainsboro,
                Left = 12,
                Top = 13,
                Font = new Font("Segoe UI", 20, FontStyle.Bold)
            };
            
            var headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 15, 8, 8),
                FlowDirection = FlowDirection.LeftToRight
            };

            exportBtn = MkBtn("Exportar", (_, __) => ExportData(), Color.FromArgb(0x22, 0xc5, 0x5e));
            importBtn = MkBtn("Importar", (_, __) => ImportData(), Color.FromArgb(245, 158, 11));
            resetBtn = MkBtn("Resetar", (_, __) => ResetData(), Color.FromArgb(0xef, 0x44, 0x44), Color.White);
            historyBtn = MkBtn("Comandas Pagas", (_, __) => OpenHistoryDialog(), Color.FromArgb(0x1d, 0x4e, 0x89), Color.White);
            restockBtn = MkBtn("Reposição", (_, __) => OpenAdvancedRestockDialog(), Color.FromArgb(34, 197, 94));

            headerActions.Controls.Add(exportBtn);
            headerActions.Controls.Add(importBtn);
            headerActions.Controls.Add(resetBtn);
            headerActions.Controls.Add(restockBtn);
            headerActions.Controls.Add(historyBtn);

            header.Controls.Add(title);
            header.Controls.Add(headerActions);
            Controls.Add(header);

            // === MAIN LAYOUT (AQUI ESTAVA O PROBLEMA PROVÁVEL) ===
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            
            // Definição das Colunas (Responsivo)
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); 
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); 
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); 
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            Controls.Add(main); // Adiciona a tabela na janela

            // ESTAS 3 LINHAS SÃO OBRIGATÓRIAS PARA OS PAINÉIS APARECEREM
            main.Controls.Add(leftPanel, 0, 0);
            main.Controls.Add(centerPanel, 1, 0);
            main.Controls.Add(rightPanel, 2, 0);

            // === PAINEL ESQUERDO (CATÁLOGO) ===
            var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  

            leftLayout.Controls.Add(MkTitle("Catálogo (Edição)"), 0, 0);

            searchBox = new TextBox { PlaceholderText = "Filtrar lista abaixo...", Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 6) };
            leftLayout.Controls.Add(searchBox, 0, 1);

            var actionsBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };

            addProductBtn = new Button { Text = "Cadastrar Produto", Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(31, 41, 55), ForeColor = Color.Gainsboro, Margin = new Padding(0, 0, 8, 0), AutoSize = true };
            addProductBtn.FlatAppearance.BorderSize = 0;

            addIngredientBtn = new Button { Text = "Cadastrar Ingrediente", Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.Gainsboro, Margin = new Padding(0, 0, 0, 0), AutoSize = true };
            addIngredientBtn.FlatAppearance.BorderSize = 0;

            actionsBar.Controls.Add(addProductBtn);
            actionsBar.Controls.Add(addIngredientBtn);

            // IMPORTANTE: Recria o container da lista para garantir que está limpo
            if (productList == null) productList = new FlowLayoutPanel();
            
            productList.Dock = DockStyle.Fill; // Ocupa todo o espaço sobrando
            productList.AutoScroll = true;     // Permite rolar
            productList.FlowDirection = FlowDirection.TopDown; // Itens um embaixo do outro
            productList.WrapContents = false;  // Não tenta jogar pro lado

            productList.Resize += (s, e) => 
            {
                foreach (Control c in productList.Controls)
                {
                    // Largura da tela - 25px (para dar espaço à barra de rolagem vertical)
                    c.Width = productList.ClientSize.Width - 25;
                }
            };
            // ------------------------------------

            // Adiciona na linha 3 da tabela da esquerda
            leftLayout.Controls.Add(actionsBar, 0, 2);
            leftLayout.Controls.Add(productList, 0, 3);
            
            leftPanel.Controls.Clear();
            leftPanel.Controls.Add(leftLayout);

        // === PAINEL DIREITO (FECHAMENTO) ===
            rightPanel.Controls.Clear();
            rightPanel.Controls.Add(MkTitle("Fechamento"));

            // 1. Grid de Estoque (Base)
            lowStockGroup = new GroupBox
            {
                Text = "⚠ Estoque crítico (0)",
                Dock = DockStyle.Bottom,
                Height = 180,
                ForeColor = Color.Gainsboro,
                Padding = new Padding(8)
            };

            lowStockGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false
            };
            lowStockGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55);
            lowStockGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            lowStockGrid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            lowStockGrid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            lowStockGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 65, 81);
            lowStockGrid.DefaultCellStyle.SelectionForeColor = Color.White;

            lowStockGrid.Columns.Clear();
            lowStockGrid.Columns.Add("tipo", "Tipo");
            lowStockGrid.Columns.Add("produto", "Produto");
            lowStockGrid.Columns.Add("estoque", "Qtd");
            lowStockGrid.Columns.Add("un", "Un.");
            lowStockGrid.Columns[0].Width = 40; 
            lowStockGrid.Columns[2].Width = 40; 
            lowStockGrid.Columns[3].Width = 35; 

            lowStockGroup.Controls.Add(lowStockGrid);
            rightPanel.Controls.Add(lowStockGroup);

            // 2. CONTEÚDO PRINCIPAL (COM ESTICAMENTO AUTOMÁTICO)
            var pnlRightContent = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false, 
                AutoScroll = true, 
                Padding = new Padding(5, 5, 5, 0)
            };

            // VARIÁVEIS DE CONTROLE
            servicePct = new NumericUpDown { Value = 10, Visible = false }; 
            discountPct = new NumericUpDown { Value = 0, Visible = false };

            // DESCONTO EM DINHEIRO
            var lblDesc = new Label { Text = "Desconto (R$)", AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = Color.Gainsboro };
            discountVal = new NumericUpDown { 
                DecimalPlaces = 2, 
                Maximum = 100000, 
                Height = 35,   
                // Largura inicial (será ajustada pelo esticamento)
                Width = 200, 
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Margin = new Padding(0, 5, 0, 15) 
            };

            // TOTAIS
            var fontLabel = new Font("Segoe UI", 11, FontStyle.Regular);
            var fontTotal = new Font("Segoe UI", 22, FontStyle.Bold); 

            subtotalLbl = new Label { Text = "Subtotal: R$ 0,00", AutoSize = true, Font = fontLabel, ForeColor = Color.LightGray, Margin = new Padding(0, 0, 0, 5) };
            serviceLbl = new Label { Text = "Serviço (10%): R$ 0,00", AutoSize = true, Font = fontLabel, ForeColor = Color.LightGray, Margin = new Padding(0, 0, 0, 5) };
            discountsLbl = new Label { Text = "Descontos: R$ 0,00", AutoSize = true, Font = fontLabel, ForeColor = Color.FromArgb(248, 113, 113), Margin = new Padding(0, 0, 0, 15) };
            
            totalLbl = new Label { 
                Text = "TOTAL: R$ 0,00", 
                AutoSize = true, 
                Font = fontTotal, 
                ForeColor = Color.FromArgb(74, 222, 128), 
                Margin = new Padding(0, 0, 0, 20) 
            };

            // GRADE DE BOTÕES (2x2)
            var buttonsGrid = new TableLayoutPanel 
            { 
                Width = 200, // Largura inicial
                Height = 140, 
                RowCount = 2,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            
            buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            buttonsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            void StyleGridBtn(Button b, Color bg) {
                b.Dock = DockStyle.Fill; 
                b.BackColor = bg;
                b.ForeColor = Color.White;
                b.Font = new Font("Segoe UI", 9, FontStyle.Bold); 
                b.Margin = new Padding(2); 
                b.Cursor = Cursors.Hand;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
            }

            var payBtn = MkBtn("PAGAR", (_, __) => OpenPaymentDialog(), Color.Transparent);
            StyleGridBtn(payBtn, Color.FromArgb(37, 99, 235)); 

            var printBtn = MkBtn("IMPRIMIR", (_, __) => ShowQuickPrintPreviewWithPrintOption(), Color.Transparent);
            StyleGridBtn(printBtn, Color.FromArgb(245, 158, 11)); 
            printBtn.ForeColor = Color.Black; 

            var cancelBtn = MkBtn("CANCELAR", (_, __) => CancelComanda(), Color.Transparent);
            StyleGridBtn(cancelBtn, Color.FromArgb(220, 38, 38)); 

            var closeBtn = MkBtn("FECHAR", (_, __) => CloseComanda(), Color.Transparent);
            StyleGridBtn(closeBtn, Color.FromArgb(22, 163, 74)); 

            buttonsGrid.Controls.Add(payBtn, 0, 0);    
            buttonsGrid.Controls.Add(printBtn, 1, 0);  
            buttonsGrid.Controls.Add(cancelBtn, 0, 1); 
            buttonsGrid.Controls.Add(closeBtn, 1, 1);  

            // Adiciona tudo ao painel
            pnlRightContent.Controls.Add(lblDesc);
            pnlRightContent.Controls.Add(discountVal);
            pnlRightContent.Controls.Add(subtotalLbl);
            pnlRightContent.Controls.Add(serviceLbl);
            pnlRightContent.Controls.Add(discountsLbl);
            pnlRightContent.Controls.Add(totalLbl);
            pnlRightContent.Controls.Add(buttonsGrid); 
            pnlRightContent.Controls.Add(servicePct); 
            pnlRightContent.Controls.Add(discountPct);

            rightPanel.Controls.Add(pnlRightContent);

            // --- O SEGREDO DO "ESTICAMENTO" NA DIREITA ---
            // Este evento garante que os botões e o input sempre ocupem a largura total
            pnlRightContent.Resize += (s, e) => {
                int w = pnlRightContent.ClientSize.Width - 15; // Margem de segurança
                if (w < 100) return;
                
                // Estica o input de desconto
                discountVal.Width = w;
                // Estica a grade de botões
                buttonsGrid.Width = w;
            };

            // === PAINEL CENTRAL (COMANDA) ===
            var centerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5 };
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Título
            centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Linha 1: Inputs
            centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Linha 2: Botões (Fica embaixo pra não cortar)
            centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Info
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid (Resto)

            // 1. TÍTULO
            var titleCenter = MkTitle("Comanda");
            titleCenter.TextAlign = ContentAlignment.MiddleCenter;
            titleCenter.Dock = DockStyle.Fill;
            centerLayout.Controls.Add(titleCenter, 0, 0);

            // 2. LINHA DE INPUTS (Quem é o cliente?)
            var rowInputs = new FlowLayoutPanel 
            { 
                AutoSize = true, 
                Anchor = AnchorStyles.Top, // Centraliza
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 5, 0, 0)
            };

            comandaSelect = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11) };
            tableInput = new TextBox { Width = 150, PlaceholderText = "Mesa/Cliente", Font = new Font("Segoe UI", 11) };
            
            rowInputs.Controls.Add(comandaSelect);
            rowInputs.Controls.Add(tableInput);

            // 3. LINHA DE BOTÕES (Agora perfeitamente alinhados)
            var rowActions = new FlowLayoutPanel 
            { 
                AutoSize = true, 
                Anchor = AnchorStyles.Top, 
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                // Adiciona um espaço extra em cima para separar dos inputs
                Margin = new Padding(0, 10, 0, 10) 
            };

            // Botão Nova (Verde)
            newComandaBtn = MkBtn("Nova", null, Color.FromArgb(34, 197, 94));
            newComandaBtn.Height = 40; // Altura igual para os dois
            newComandaBtn.Width = 100; 
            // Margin: Top=0 para alinhar, Right=10 para afastar do azul
            newComandaBtn.Margin = new Padding(0, 0, 10, 0); 

            // Botão Adicionar Itens (Azul)
            btnAddItems = MkBtn("+ Adicionar Itens (F2)", (_, __) => OpenAddItemsDialog(), Color.FromArgb(59, 130, 246), Color.White);
            btnAddItems.Height = 40; // Altura igual
            btnAddItems.Width = 220; 
            btnAddItems.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            // Margin: Top=0 é OBRIGATÓRIO para alinhar com o botão verde
            btnAddItems.Margin = new Padding(0, 0, 0, 0); 

            rowActions.Controls.Add(newComandaBtn);
            rowActions.Controls.Add(btnAddItems);

            // 4. INFO DA COMANDA
            comandaInfo = new Label 
            { 
                Text = "Nenhuma comanda selecionada.", 
                AutoSize = true, 
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 0, 0, 5) 
            };

            // 5. GRID DE ITENS
            itemsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            
            // Estilos do Grid
            itemsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55);
            itemsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            itemsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            itemsGrid.ColumnHeadersHeight = 35;
            
            itemsGrid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            itemsGrid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            itemsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 65, 81);
            itemsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            itemsGrid.GridColor = Color.FromArgb(55, 65, 81);

            itemsGrid.Columns.Clear();
            itemsGrid.Columns.Add("name", "Item");
            itemsGrid.Columns.Add("qty", "Qtd");
            itemsGrid.Columns.Add("price", "Preço");
            itemsGrid.Columns.Add("total", "Total");
            
            // Botões do Grid (-, +, X)
            var btnColStyle = new DataGridViewButtonColumn 
            { 
                FlatStyle = FlatStyle.Flat, 
                DefaultCellStyle = { BackColor = Color.Transparent, ForeColor = Color.LightGray } 
            };
            var colMinus = (DataGridViewButtonColumn)btnColStyle.Clone(); colMinus.Name = "minus"; colMinus.Text = "-"; colMinus.UseColumnTextForButtonValue = true; colMinus.Width = 30;
            var colPlus = (DataGridViewButtonColumn)btnColStyle.Clone(); colPlus.Name = "plus"; colPlus.Text = "+"; colPlus.UseColumnTextForButtonValue = true; colPlus.Width = 30;
            var colRemove = (DataGridViewButtonColumn)btnColStyle.Clone(); colRemove.Name = "remove"; colRemove.Text = "X"; colRemove.UseColumnTextForButtonValue = true; colRemove.Width = 30;

            itemsGrid.Columns.Add(colMinus);
            itemsGrid.Columns.Add(colPlus);
            itemsGrid.Columns.Add(colRemove);

            // Adicionando tudo ao layout principal
            centerLayout.Controls.Add(rowInputs, 0, 1);
            centerLayout.Controls.Add(rowActions, 0, 2);
            centerLayout.Controls.Add(comandaInfo, 0, 3);
            centerLayout.Controls.Add(itemsGrid, 0, 4);
            
            centerPanel.Controls.Clear();
            centerPanel.Controls.Add(centerLayout);

            // Margens de segurança
            leftPanel.Margin = new Padding(0, 80, 0, 0);
            centerPanel.Margin = new Padding(0, 80, 0, 0);
            rightPanel.Margin = new Padding(0, 80, 0, 0);
            
            // Reconecta eventos que podem ter sido perdidos
            searchBox.TextChanged += (_, __) => { RefreshProducts(); UpdateLowStockWidget(); };
            addProductBtn.Click += (_, __) => ShowProductForm();
            addIngredientBtn.Click += (_, __) => ShowIngredientForm();
            newComandaBtn.Click += (_, __) => CreateComanda();
            comandaSelect.SelectedIndexChanged += (_, __) => LoadComandaFromSelect();
            itemsGrid.CellClick += ItemsGrid_CellClick;
            servicePct.ValueChanged += (_, __) => CalcTotalsPersist();
        }

       private void Init()
        {
            // 1. Tenta carregar os dados
            DataStore.Load(); 
            
            // 2. DIAGNÓSTICO: Verifica se tem produtos
            int qtd = DataStore.Products.Count;
            
            if (qtd == 0)
            {
                MessageBox.Show("O banco de dados está VAZIO (0 produtos).\nVou criar um 'Produto Teste' agora.", "Diagnóstico");
                
                // Cria um produto forçado para garantir que algo apareça
                var pTeste = new Product 
                { 
                    Id = 999, 
                    Name = "PRODUTO TESTE", 
                    Price = 10.00m, 
                    Stock = 50, 
                    Category = "Lanches" 
                };
                DataStore.Products.Add(pTeste);
                DataStore.Save(); // Salva para não perder
            }
            else
            {
                // Se cair aqui, os produtos existem, então é erro visual
                // MessageBox.Show($"Sucesso! {qtd} produtos carregados.", "Diagnóstico");
            }

            // 3. Atualiza a tela
            RefreshProducts(); 
            RefreshComandasSelect();
            UpdateLowStockWidget();

            // Cria comanda 1 se não existir
            if (!DataStore.Comandas.Any(c => c.Status == "open"))
            {
                var id = DataStore.NextComandaId();
                DataStore.Comandas.Add(new Comanda { Id = id, TableName = "Mesa 1", Status = "open", CreatedAt = DateTime.Now, Items = new List<ComandaItem>() });
                DataStore.Save();
                RefreshComandasSelect();
            }
        }
        
        private void OpenAddItemsDialog()
        {
            if (currentComandaId == null)
            {
                MessageBox.Show("Selecione ou crie uma comanda primeiro.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dlg = new ProductDialog(); 
            
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var item in dlg.SelectedItems)
                {
                    DataStore.TryAddItemToComanda(currentComandaId.Value, item.ProductId, item.Qty, out _);
                }
                LoadComanda(currentComandaId.Value);
                RefreshProducts();
                UpdateLowStockWidget();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                OpenAddItemsDialog();
                return true; 
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenHistoryDialog()
        {
            var comandasFechadas = DataStore.Comandas
                .Where(c => c.Status == "closed" || c.Status == "canceled")
                .OrderByDescending(c => c.ClosedAt)
                .ToList();

            if (!comandasFechadas.Any())
            {
                MessageBox.Show("Não há histórico de comandas ainda.", "Histórico Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new ModernHistoryDialog(comandasFechadas))
            {
                dlg.ShowDialog(this);
            }
        }

        private void OpenPaymentDialog()
        {
            if (currentComandaId == null)
            {
                MessageBox.Show("Selecione uma comanda aberta.");
                return;
            }

            var c = DataStore.Comandas.First(x => x.Id == currentComandaId);
            CalcTotalsPersist(); 

            using (var dlg = new PaymentDialog(c))
            {
                var result = dlg.ShowDialog(this);
                if (dlg.PaymentMade)
                {
                    CalcTotalsPersist(); 
                    if (c.Totals.Remaining <= 0)
                    {
                        var q = MessageBox.Show($"A comanda #{c.Id} foi totalmente paga.\nDeseja fechar a mesa agora?", "Pagamento Completo", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (q == DialogResult.Yes) CloseComanda(); 
                    }
                    else LoadComanda(c.Id); 
                }
            }
        }

        private void CloseComanda()
        {
            if (currentComandaId == null)
            {
                MessageBox.Show("Selecione uma comanda para fechar.");
                return;
            }

            var c = DataStore.Comandas.First(x => x.Id == currentComandaId);
            CalcTotalsPersist();

            if (c.Totals.Remaining > 0.05m) 
            {
                var result = MessageBox.Show($"Ainda falta pagar {c.Totals.Remaining:C}.\nDeseja abrir a tela de pagamento?", 
                                             "Comanda Aberta", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) OpenPaymentDialog(); 
                return; 
            }

            if (!DataStore.CloseComandaSafe(c.Id, out var err))
            {
                MessageBox.Show(err);
                return;
            }

            MessageBox.Show($"Comanda #{c.Id} encerrada com sucesso!");
            RefreshProducts();
            RefreshComandasSelect();
            UpdateLowStockWidget();
        }

        private void ShowQuickPrintPreviewWithPrintOption()
        {
            if (currentComandaId == null)
            {
                MessageBox.Show("Selecione uma comanda.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var comanda = DataStore.Comandas.FirstOrDefault(c => c.Id == currentComandaId.Value);
            if (comanda == null) return;

            CalcTotalsPersist();

            using (var dlg = new ModernPrintPreviewDialog(comanda))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    StartRealPrintFlow();
                }
            }
        }

        private bool IsComposite(Product p) => DataStore.Recipes.Any(r => r.ParentProductId == p.Id);

        private bool IsIngredient(Product p)
        {
            var cat = (p.Category ?? "").Trim();
            bool catMatch = cat.Equals("Ingredientes", StringComparison.OrdinalIgnoreCase);
            bool usedAsIngredient = DataStore.Recipes.Any(r => r.IngredientProductId == p.Id);
            return catMatch || usedAsIngredient;
        }

        private bool IsTableNameInUse(string tableName)
        {
            var name = (tableName ?? "").Trim();
            if (name.Length == 0) return false;
            return DataStore.Comandas.Any(c =>
                string.Equals(c.Status, "open", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((c.TableName ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        private int CalculateCompositeStock(Product p)
        {
            var recipe = DataStore.Recipes.Where(r => r.ParentProductId == p.Id).ToList();
            if (recipe.Count == 0) return 0;

            int maxPossible = int.MaxValue; 

            foreach (var item in recipe)
            {
                var ingredient = DataStore.Products.FirstOrDefault(x => x.Id == item.IngredientProductId);
                if (ingredient == null || ingredient.Stock <= 0) return 0;
                int canMake = ingredient.Stock / item.QuantityPerUnit;
                if (canMake < maxPossible) maxPossible = canMake;
            }

            return maxPossible == int.MaxValue ? 0 : maxPossible;
        }

      private void RefreshProducts()
        {
            if (productList == null || productList.IsDisposed) return;

            productList.SuspendLayout();
            productList.Controls.Clear();
            productList.BackColor = Color.Transparent;

            int scrollSpace = 25; 
            int w = productList.ClientSize.Width - scrollSpace;
            if (w <= 0) w = 240; 

            var f = (searchBox.Text ?? "").Trim().ToLowerInvariant();

            var lista = DataStore.Products
                .Where(p => !string.Equals(p.Category, "Ingredientes", StringComparison.OrdinalIgnoreCase))
                .Where(p => string.IsNullOrWhiteSpace(f) || 
                            p.Name.ToLower().Contains(f) ||         
                            (p.Category ?? "").ToLower().Contains(f))
                .OrderBy(p => p.Name);

            foreach (var p in lista)
            {
                // USANDO O NOVO PAINEL
                var panel = new RoundedPanel
                {
                    Width = w, 
                    Height = 110,
                    
                    // --- AQUI A CONFIGURAÇÃO CORRETA ---
                    // BackColor fica transparente (automático na classe)
                    // CorDeFundo define a cor do card (Opção 3 - Glassy Dark)
                    CorDeFundo = Color.FromArgb(30, 30, 35), 
                    
                    Radius = 30, 
                    Padding = new Padding(10),
                    Margin = new Padding(0, 0, 0, 10)
                };

                bool isComposite = IsComposite(p);
                int displayStock = isComposite ? CalculateCompositeStock(p) : p.Stock;

                // NOME
                var name = new Label 
                { 
                    Text = p.Name, 
                    AutoSize = false, 
                    ForeColor = Color.WhiteSmoke, // Branco suave
                    Font = new Font("Segoe UI", 11, FontStyle.Bold), 
                    Location = new Point(15, 12),
                    Size = new Size(w - 130, 45), 
                    TextAlign = ContentAlignment.TopLeft,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right 
                };

                // PREÇO
                var price = new Label
                {
                    Text = p.Price.ToString("C"),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(74, 222, 128), // Verde Destaque
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    Location = new Point(w - 100, 12), 
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    TextAlign = ContentAlignment.TopRight
                };
                price.Left = w - price.PreferredWidth - 15; 

                // INFO
                string stockInfo = isComposite ? $"Produz: {displayStock} un" : $"Estoque: {displayStock} {p.Unit}";
                var meta = new Label
                {
                    Text = $"{p.Category} • {stockInfo}",
                    AutoSize = false,
                    ForeColor = Color.FromArgb(160, 160, 180), // Cinza levemente azulado
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    Location = new Point(15, 60), 
                    Size = new Size(w - 140, 45),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                // BOTÃO (Opção 3 - Roxo Indigo)
                var addBtn = MkBtn("Adicionar", (_, __) => AddProductToCurrentComanda(p, 1));
                
                // COR DO BOTÃO
                addBtn.BackColor = Color.FromArgb(99, 102, 241); 
                addBtn.ForeColor = Color.White;
                addBtn.FlatStyle = FlatStyle.Flat;
                addBtn.FlatAppearance.BorderSize = 0;
                
                addBtn.Size = new Size(100, 32); 
                addBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                addBtn.Location = new Point(w - 120, 65); 

                panel.Controls.Add(name); 
                panel.Controls.Add(meta); 
                panel.Controls.Add(price); 
                panel.Controls.Add(addBtn);
                
                productList.Controls.Add(panel);
            }
            productList.ResumeLayout();
        }

        private void UpdateLowStockWidget()
        {
            var criticos = DataStore.Products
                .Where(p => p.Stock <= LOW_STOCK_THRESHOLD && (IsIngredient(p) || !IsComposite(p)))
                .OrderBy(p => IsIngredient(p) ? "Ingrediente" : (p.Category ?? "").Trim())
                .ThenBy(p => p.Name)
                .Select(p => new { Tipo = IsIngredient(p) ? "Ingrediente" : (p.Category ?? "").Trim(), Produto = p.Name, Estoque = p.Stock, Unid = p.Unit })
                .ToList();

            lowStockGrid.Rows.Clear();
            foreach (var it in criticos) lowStockGrid.Rows.Add(it.Tipo, it.Produto, it.Estoque, it.Unid);
            lowStockGroup.Text = $"⚠ Estoque crítico ({criticos.Count})";
        }

        private void OpenAdvancedRestockDialog()
        {
            using var dlg = new AdvancedRestockWindow(null, LOW_STOCK_THRESHOLD);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var line in dlg.Result)
                {
                    var p = DataStore.Products.FirstOrDefault(x => x.Id == line.ProductId);
                    if (p == null) continue;
                    int plus  = Math.Max(0, line.Recebido);
                    int minus = Math.Max(0, line.AjusteNegativo);
                    if (plus > 0)
                    {
                        p.Stock += plus;
                        DataStore.StockMov.Add(new StockMovement { Id = DataStore.NextMovId(), Type = "in", ProductId = p.Id, Qty = plus, Timestamp = DateTime.Now });
                    }
                    if (minus > 0)
                    {
                        int realMinus = Math.Min(minus, p.Stock);
                        p.Stock -= realMinus;
                        DataStore.StockMov.Add(new StockMovement { Id = DataStore.NextMovId(), Type = "out", ProductId = p.Id, Qty = realMinus, Timestamp = DateTime.Now });
                    }
                }
                DataStore.Save();
                RefreshProducts();
                UpdateLowStockWidget();
                MessageBox.Show("Reposição aplicada!");
            }
        }

        private void StartRealPrintFlow()
        {
            if (currentComandaId == null) return; 
            CalcTotalsPersist(); 
            try { printPreviewDialog.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show("Erro ao iniciar a impressão: " + ex.Message, "Erro de Impressão", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ComandaPrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            var comanda = DataStore.Comandas.FirstOrDefault(c => c.Id == currentComandaId);
            if (comanda == null || e.Graphics == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Configurações de Fonte
            Font fTitle = new Font("Segoe UI", 16, FontStyle.Bold);
            Font fSubtitle = new Font("Segoe UI", 10, FontStyle.Regular);
            Font fHeader = new Font("Segoe UI", 9, FontStyle.Bold);
            Font fBody = new Font("Segoe UI", 9, FontStyle.Regular);
            Font fTotal = new Font("Segoe UI", 12, FontStyle.Bold);
            Font fBigTotal = new Font("Segoe UI", 16, FontStyle.Bold);

            // Cores e Pincéis
            Brush bBlack = Brushes.Black;
            Brush bGray = Brushes.DarkGray;
            Pen pLine = new Pen(Color.Black, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash }; // Linha pontilhada

            // Margens e Largura (Simula um cupom centralizado na folha A4)
            int paperWidth = 300; // Largura típica de cupom
            int startX = (e.PageBounds.Width - paperWidth) / 2; // Centraliza na folha
            int startY = 40;
            int y = startY;
            int rightX = startX + paperWidth;

            // Alinhamentos
            StringFormat center = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat right = new StringFormat { Alignment = StringAlignment.Far };
            StringFormat left = new StringFormat { Alignment = StringAlignment.Near };

            // === 1. CABEÇALHO ===
            g.DrawString("MVP RESTAURANTE", fTitle, bBlack, startX + (paperWidth / 2), y, center);
            y += 30;
            g.DrawString("Comprovante de Venda", fSubtitle, bGray, startX + (paperWidth / 2), y, center);
            y += 25;
            g.DrawLine(pLine, startX, y, rightX, y); // Linha ---------
            y += 10;

            // === 2. DADOS DA COMANDA ===
            void DrawRow(string label, string val, Font f, Brush b)
            {
                g.DrawString(label, f, b, startX, y, left);
                g.DrawString(val, f, b, rightX, y, right);
                y += 20;
            }

            DrawRow("Comanda:", $"#{comanda.Id}", fHeader, bBlack);
            DrawRow("Mesa/Cliente:", comanda.TableName, fBody, bBlack);
            DrawRow("Data:", comanda.CreatedAt.ToString("dd/MM/yyyy HH:mm"), fBody, bBlack);
            
            y += 5;
            g.DrawLine(pLine, startX, y, rightX, y); // Linha ---------
            y += 5;

            // === 3. TABELA DE ITENS ===
            // Cabeçalhos
            g.DrawString("ITEM", fHeader, bGray, startX, y);
            g.DrawString("QTD", fHeader, bGray, startX + 180, y, center); // Qtd no meio
            g.DrawString("TOTAL", fHeader, bGray, rightX, y, right);
            y += 20;

            // Itens
            foreach (var item in comanda.Items)
            {
                // Nome do item (pode ser longo, vamos limitar a largura)
                RectangleF rectName = new RectangleF(startX, y, 160, 40); 
                g.DrawString(item.Name, fBody, bBlack, rectName);

                // Qtd
                g.DrawString($"{item.Qty}x", fBody, bBlack, startX + 180, y, center);

                // Valor Total do Item
                g.DrawString((item.Qty * item.Price).ToString("C"), fBody, bBlack, rightX, y, right);
                
                y += 20;
            }

            y += 10;
            g.DrawLine(pLine, startX, y, rightX, y); // Linha ---------
            y += 10;

            // === 4. TOTAIS ===
            var t = comanda.Totals;

            DrawRow("Subtotal", t.Subtotal.ToString("C"), fBody, bBlack);
            
            if (t.Service > 0)
                DrawRow($"Serviço ({comanda.ServicePct}%)", t.Service.ToString("C"), fBody, bBlack);
            
            if (t.Discounts > 0)
                DrawRow("Descontos", "-" + t.Discounts.ToString("C"), fBody, Brushes.Red);

            y += 10;
            
            // TOTAL FINAL GRANDE
            g.DrawString("TOTAL FINAL", fTotal, bBlack, startX, y + 5);
            g.DrawString(t.Total.ToString("C"), fBigTotal, Brushes.Green, rightX, y, right);
            y += 40;

            // === 5. PAGAMENTOS E TROCO ===
            if (t.Paid > 0)
            {
                g.DrawLine(pLine, startX, y, rightX, y);
                y += 5;
                g.DrawString("Pagamentos Realizados:", fHeader, bGray, startX, y);
                y += 20;

                foreach (var p in comanda.Payments)
                {
                    DrawRow("- " + p.Method, p.Amount.ToString("C"), fBody, bBlack);
                }

                y += 10;
                if (t.Remaining > 0)
                {
                    g.DrawString("FALTA PAGAR", fTotal, Brushes.Orange, startX, y);
                    g.DrawString(t.Remaining.ToString("C"), fTotal, Brushes.Orange, rightX, y, right);
                }
                else
                {
                    g.DrawString("TROCO / QUITADO", fHeader, bGray, startX, y);
                    // Se pagou a mais, mostra troco hipotético (se tivesse guardado essa info)
                    // Como o sistema calcula o troco na hora, aqui mostramos Status OK
                    g.DrawString("CONTA PAGA ✔", fTotal, bBlack, rightX, y, right);
                }
                y += 30;
            }

            // === 6. RODAPÉ ===
            y += 20;
            g.DrawLine(Pens.Black, startX, y, rightX, y); // Linha sólida final
            y += 5;
            g.DrawString("Obrigado pela preferência!", fSubtitle, bGray, startX + (paperWidth / 2), y, center);
            g.DrawString("Volte sempre.", fSubtitle, bGray, startX + (paperWidth / 2), y + 20, center);

            e.HasMorePages = false;
        }

        private void ShowProductForm(Product? existing = null)
        {
            var dlg = new ProductEditorDialog(existing); 
            
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultProduct != null)
            {
                var p = dlg.ResultProduct;
                if (existing == null) DataStore.Products.Add(p);
                else
                {
                    var idx = DataStore.Products.FindIndex(x => x.Id == existing.Id);
                    if (idx >= 0) DataStore.Products[idx] = p;
                }

                DataStore.Recipes.RemoveAll(r => r.ParentProductId == p.Id);
                DataStore.Recipes.AddRange(dlg.ResultRecipe);

                DataStore.Save();
                RefreshProducts();
                UpdateLowStockWidget();
            }
        }

        private void ShowIngredientForm()
        {
            var dlg = new ProductEditorDialog();
            dlg.PresetAsIngredient(); 
            
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultProduct != null)
            {
                var p = dlg.ResultProduct;
                DataStore.Products.Add(p);
                DataStore.Save();
                RefreshProducts();
                UpdateLowStockWidget();
            }
        }

        private void RefreshComandasSelect()
        {
            comandaSelect.Items.Clear();
            foreach (var c in DataStore.Comandas.Where(c => c.Status == "open"))
                comandaSelect.Items.Add($"#{c.Id} — {c.TableName}");

            if (comandaSelect.Items.Count > 0)
            {
                comandaSelect.SelectedIndex = 0;
                LoadComandaFromSelect();
            }
            else
            {
                currentComandaId = null;
                comandaInfo.Text = "Nenhuma comanda selecionada.";
                itemsGrid.Rows.Clear();
                CalcTotalsPersist();
            }
        }

        private void LoadComandaFromSelect()
        {
            if (comandaSelect.SelectedItem == null) return;
            var text = comandaSelect.SelectedItem.ToString()!;
            var idStr = text.Split('—')[0].Trim().TrimStart('#');
            if (int.TryParse(idStr, out int id)) LoadComanda(id);
        }

        private void CreateComanda()
        {
            var table = (tableInput.Text ?? "").Trim();
            if (string.IsNullOrEmpty(table))
            {
                MessageBox.Show("Informe a mesa/cliente.");
                tableInput.Focus();
                return;
            }

            if (IsTableNameInUse(table))
            {
                MessageBox.Show($"Já existe uma comanda ABERTA chamada \"{table}\".\nUse outro nome ou selecione a existente.", "Nome duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tableInput.SelectAll();
                tableInput.Focus();
                return;
            }

            var id = DataStore.NextComandaId();
            DataStore.Comandas.Add(new Comanda
            {
                Id = id,
                TableName = table,
                Status = "open",
                CreatedAt = DateTime.Now,
                Items = new List<ComandaItem>(),
                ServicePct = 10m,
                DiscountPct = 0m,
                DiscountValue = 0m,
                Totals = new Totals()
            });
            DataStore.Save();
            RefreshComandasSelect();
            comandaSelect.SelectedItem = $"#{id} — {table}";
            LoadComanda(id);
        }

        private void CancelComanda()
        {
            if (currentComandaId == null) return;
            var ok = MessageBox.Show("Cancelar esta comanda? Itens voltarão ao estoque.", "Cancelar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes) return;

            if (!DataStore.CancelComanda(currentComandaId.Value, out var err))
            {
                MessageBox.Show(err);
                return;
            }
            RefreshComandasSelect();
            ClearComandaView();
            RefreshProducts();
            UpdateLowStockWidget();
        }

        private void LoadComanda(int id)
        {
            currentComandaId = id;
            var c = DataStore.Comandas.FirstOrDefault(c => c.Id == id);
            if (c == null) return;

            comandaInfo.Text = $"Comanda #{c.Id} — Mesa/Cliente: {c.TableName} — Aberta em {c.CreatedAt}";
            servicePct.Value = Math.Min(Math.Max((decimal)c.ServicePct, 0), 20);
            discountVal.Value = Math.Max(0, (decimal)c.DiscountValue);
            discountPct.Value = Math.Min(Math.Max((decimal)c.DiscountPct, 0), 100);

            RenderItems(c);
            CalcTotalsPersist();
        }

        private void RenderItems(Comanda c)
        {
            itemsGrid.Rows.Clear();
            foreach (var it in c.Items)
                itemsGrid.Rows.Add(it.Name, it.Qty, it.Price.ToString("C"), (it.Price * it.Qty).ToString("C"));
        }

        private void AddProductToCurrentComanda(Product p, int qty = 1)
        {
            if (currentComandaId == null)
            {
                MessageBox.Show("Selecione ou crie uma comanda.");
                return;
            }
            if (!DataStore.TryAddItemToComanda(currentComandaId.Value, p.Id, qty, out var err))
            {
                MessageBox.Show(err, "Estoque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadComanda(currentComandaId.Value);
            RefreshProducts();
            UpdateLowStockWidget();
        }

        private void RemoveItemFlow(Comanda c, ComandaItem it)
        {
            int qtyToRemove = 1;
            if (it.Qty > 1)
            {
                var input = Interaction.InputBox($"\"{it.Name}\" na comanda: {it.Qty}\nQuantas deseja remover?", "Remover quantidade", "1");
                if (!int.TryParse(input, out qtyToRemove) || qtyToRemove <= 0) return;
                if (qtyToRemove > it.Qty)
                {
                    MessageBox.Show($"Quantidade inválida.");
                    return;
                }
            }
            else
            {
                var ok = MessageBox.Show($"Remover \"{it.Name}\"?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return;
            }

            if (!DataStore.TryRemoveItemFromComanda(c.Id, it.ProductId, qtyToRemove, out var err))
            {
                MessageBox.Show(err);
                return;
            }
            LoadComanda(c.Id);
            RefreshProducts();
            UpdateLowStockWidget();
        }

        private void ItemsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || currentComandaId == null) return;
            var c = DataStore.Comandas.First(x => x.Id == currentComandaId);
            var name = itemsGrid.Rows[e.RowIndex].Cells["name"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;
            var it = c.Items.FirstOrDefault(x => x.Name == name);
            if (it == null) return;

            var colName = itemsGrid.Columns[e.ColumnIndex].Name;
            if (colName == "minus") RemoveItemFlow(c, it);
            else if (colName == "plus")
            {
                var p = DataStore.Products.FirstOrDefault(x => x.Id == it.ProductId);
                if (p != null) AddProductToCurrentComanda(p, 1);
            }
            else if (colName == "remove") RemoveItemFlow(c, it);
        }

        private void CalcTotalsPersist()
        {
            if (currentComandaId == null)
            {
                subtotalLbl.Text = "Subtotal: R$ 0,00";
                serviceLbl.Text = "Serviço: R$ 0,00";
                discountsLbl.Text = "Descontos: R$ 0,00";
                totalLbl.Text = "Total: R$ 0,00";
                return;
            }
            var c = DataStore.Comandas.First(x => x.Id == currentComandaId);
            c.ServicePct = servicePct.Value;
            c.DiscountValue = discountVal.Value;
            c.DiscountPct = discountPct.Value;
            var t = DataStore.CalculateTotals(c);
            subtotalLbl.Text = $"Subtotal: {t.Subtotal:C}";
            serviceLbl.Text = $"Serviço: {t.Service:C}";
            discountsLbl.Text = $"Descontos: -{t.Discounts:C}";
            totalLbl.Text = $"Total: {t.Total:C}";
            DataStore.Save();
        }

        private void ClearComandaView()
        {
            currentComandaId = null;
            itemsGrid.Rows.Clear();
            subtotalLbl.Text = "Subtotal: R$ 0,00";
            serviceLbl.Text = "Serviço: R$ 0,00";
            discountsLbl.Text = "Descontos: R$ 0,00";
            totalLbl.Text = "Total: R$ 0,00";
        }

        private void ExportData() => MessageBox.Show("Função de exportar ainda não implementada.");

        private void ImportData()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Importar dados",
                Filter = "Excel (*.xlsx)|*.xlsx|Arquivos JSON (*.json)|*.json",
                Multiselect = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var firstExt = Path.GetExtension(ofd.FileNames.First()).ToLowerInvariant();

            if (firstExt == ".xlsx")
            {
                try
                {
                    DataStore.LoadFromExcelToMemory(ofd.FileName);
                    DataStore.Save();
                    
                    RefreshProducts();
                    RefreshComandasSelect();
                    UpdateLowStockWidget();

                    MessageBox.Show(
                        $"Importação Finalizada com Sucesso!\n\n" +
                        $"📦 Produtos no Sistema: {DataStore.Products.Count}\n" +
                        $"🔗 Receitas/Fichas Técnicas: {DataStore.Recipes.Count}\n\n" +
                        $"Os dados foram salvos.", 
                        "Relatório de Importação", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro CRÍTICO ao importar Excel: {ex.Message}\n\nVerifique se o arquivo não está aberto em outro programa.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (firstExt == ".json")
            {
                try
                {
                    int arquivosLidos = 0;
                    foreach (var file in ofd.FileNames)
                    {
                        var name = Path.GetFileName(file).ToLowerInvariant();
                        var json = File.ReadAllText(file);

                        if (name.Contains("product")) 
                        {
                            DataStore.Products = System.Text.Json.JsonSerializer.Deserialize<List<Product>>(json) ?? new();
                            arquivosLidos++;
                        }
                        else if (name.Contains("recipe")) 
                        {
                            DataStore.Recipes = System.Text.Json.JsonSerializer.Deserialize<List<RecipeComponent>>(json) ?? new();
                            arquivosLidos++;
                        }
                        else if (name.Contains("comanda")) 
                        {
                            DataStore.Comandas = System.Text.Json.JsonSerializer.Deserialize<List<Comanda>>(json) ?? new();
                            arquivosLidos++;
                        }
                        else if (name.Contains("stock")) 
                        {
                            DataStore.StockMov = System.Text.Json.JsonSerializer.Deserialize<List<StockMovement>>(json) ?? new();
                            arquivosLidos++;
                        }
                    }

                    DataStore.Save();
                    RefreshProducts();
                    RefreshComandasSelect();
                    UpdateLowStockWidget();
                    
                    MessageBox.Show($"Backup restaurado! {arquivosLidos} arquivos processados.", "Sucesso");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao importar JSON: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ResetData()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/RestauranteMVP";
            if (Directory.Exists(basePath))
            {
                foreach (var f in Directory.GetFiles(basePath, "*.json")) File.Delete(f);
            }
            DataStore.Load();
            RefreshProducts();
            RefreshComandasSelect();
            UpdateLowStockWidget();
            MessageBox.Show("Banco resetado.");
        }
     

        // CLASSES AUXILIARES (Restock, Print, etc.)
        internal class AdvancedRestockWindow : Form
        {
            private readonly int _critical;

            private DataGridView grid = new();
            private TextBox txtSearch = new();
            private ComboBox cboScope = new(); 
            private NumericUpDown upDefaultTarget = new();
            private Button btnSuggest = new();
            private Button btnImportCsv = new();
            private Label lblSummary = new();
            private Button okBtn = new(), cancelBtn = new();

            private class EditState { public int Plus; public int Minus; public int Target; }
            private readonly Dictionary<int, EditState> edits = new();

        public class RestockLine
        {
            public int ProductId { get; set; }
            public int Recebido { get; set; }
            public int AjusteNegativo { get; set; }
        }
        public List<RestockLine> Result { get; private set; } = new();

        public AdvancedRestockWindow(object? dbIgnored, int lowStockThreshold)
        {
            _critical = lowStockThreshold;
            Text = "Reposição (Avançada)";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; 
            MaximizeBox = false; MinimizeBox = false;
            Width = 1100; Height = 640;

            BuildUI();
            RebuildGrid();
            UpdateSummary();
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      
            Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Padding = new Padding(8), FlowDirection = FlowDirection.LeftToRight };
            cboScope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cboScope.Items.AddRange(new object[] { "Somente críticos", "Todos", "Somente ingredientes", "Somente produtos" });
            cboScope.SelectedIndex = 0;
            cboScope.SelectedIndexChanged += (_, __) => { RebuildGrid(); };

            txtSearch = new TextBox { PlaceholderText = "Buscar nome/categoria…", Width = 280, Margin = new Padding(8, 2, 8, 2) };
            txtSearch.TextChanged += (_, __) => RebuildGrid();

            var lblTarget = new Label { Text = "Alvo padrão:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) };
            upDefaultTarget = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 20, Width = 90, Margin = new Padding(0, 2, 8, 2) };

            btnSuggest = new Button { Text = "Sugerir até alvo", AutoSize = true };
            btnSuggest.Click += (_, __) => SuggestToTarget();

            btnImportCsv = new Button { Text = "Importar CSV…", AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            btnImportCsv.Click += (_, __) => ImportCsv();

            top.Controls.Add(cboScope); top.Controls.Add(txtSearch); top.Controls.Add(lblTarget); top.Controls.Add(upDefaultTarget); top.Controls.Add(btnSuggest); top.Controls.Add(btnImportCsv);
            root.Controls.Add(top, 0, 0);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(17, 24, 39), BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false, ReadOnly = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 65, 81);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "id", HeaderText = "Id", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "tipo", HeaderText = "Tipo", FillWeight = 55, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "nome", HeaderText = "Produto/Ingrediente", FillWeight = 200, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "cat", HeaderText = "Categoria", FillWeight = 90, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "estoque", HeaderText = "Estoque", FillWeight = 55, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "un", HeaderText = "Un.", FillWeight = 40, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "alvo", HeaderText = "Alvo", FillWeight = 55 });    
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "plus", HeaderText = "Recebido (+)", FillWeight = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "minus", HeaderText = "Ajuste (-)", FillWeight = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "res", HeaderText = "Resultado", FillWeight = 65, ReadOnly = true });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bPlus1",  HeaderText = "", Text = "+1",  UseColumnTextForButtonValue = true, FillWeight = 35 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bPlus5",  HeaderText = "", Text = "+5",  UseColumnTextForButtonValue = true, FillWeight = 35 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bPlus10", HeaderText = "", Text = "+10", UseColumnTextForButtonValue = true, FillWeight = 40 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bMinus1", HeaderText = "", Text = "-1",  UseColumnTextForButtonValue = true, FillWeight = 35 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bMinus5", HeaderText = "", Text = "-5",  UseColumnTextForButtonValue = true, FillWeight = 35 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bToTarget", HeaderText = "", Text = "Até alvo", UseColumnTextForButtonValue = true, FillWeight = 60 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "bKeypad", HeaderText = "", Text = "Teclado…", UseColumnTextForButtonValue = true, FillWeight = 70 });

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.CellContentClick += Grid_CellContentClick;
            grid.CellFormatting += Grid_CellFormatting;
            root.Controls.Add(grid, 0, 1);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            lblSummary = new Label { AutoSize = true, Text = "0 itens • +0 • -0" };
            footer.Controls.Add(lblSummary, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            okBtn = new Button { Text = "Aplicar", AutoSize = true };
            okBtn.Click += (_, __) => Confirm();
            cancelBtn = new Button { Text = "Cancelar", AutoSize = true };
            cancelBtn.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(okBtn); actions.Controls.Add(cancelBtn);
            footer.Controls.Add(actions, 1, 0);
            root.Controls.Add(footer, 0, 2);
        }

        private bool IsComposite(Product p) => DataStore.Recipes.Any(r => r.ParentProductId == p.Id);
        private bool IsIngredient(Product p)
        {
            var cat = (p.Category ?? "").Trim();
            return cat.Equals("Ingredientes", StringComparison.OrdinalIgnoreCase) || DataStore.Recipes.Any(r => r.IngredientProductId == p.Id);
        }

        private IEnumerable<Product> Source()
        {
            IEnumerable<Product> src = DataStore.Products;
            switch (cboScope.SelectedIndex)
            {
                case 0: src = src.Where(p => p.Stock <= _critical && (IsIngredient(p) || !IsComposite(p))); break;
                case 1: src = src.Where(p => IsIngredient(p) || !IsComposite(p)); break;
                case 2: src = src.Where(p => IsIngredient(p)); break;
                case 3: src = src.Where(p => !IsComposite(p) && !IsIngredient(p)); break;
            }
            var f = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(f)) src = src.Where(p => ($"{p.Name} {p.Category}").ToLower().Contains(f));
            return src.OrderBy(p => p.Stock <= _critical ? 0 : 1).ThenBy(p => p.Name);
        }

        private void RebuildGrid()
        {
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (r.IsNewRow) continue;
                int id = AsInt(r.Cells["id"].Value);
                var st = GetState(id);
                st.Target = AsInt(r.Cells["alvo"].Value, st.Target);
                st.Plus = AsInt(r.Cells["plus"].Value, st.Plus);
                st.Minus = AsInt(r.Cells["minus"].Value, st.Minus);
            }
            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (var p in Source())
            {
                var st = GetState(p.Id);
                if (st.Target <= 0) st.Target = (int)upDefaultTarget.Value;
                var tipo = IsIngredient(p) ? "Ingrediente" : "Produto";
                int result = Math.Max(0, p.Stock + st.Plus - st.Minus);
                grid.Rows.Add(p.Id, tipo, p.Name, p.Category, p.Stock, p.Unit, st.Target, st.Plus, st.Minus, result, "+1", "+5", "+10", "-1", "-5", "Até alvo", "Teclado…");
            }
            grid.ResumeLayout();
            UpdateSummary();
            grid.Invalidate();
        }

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            int id = AsInt(row.Cells["id"].Value);
            var st = GetState(id);
            st.Target = Math.Max(0, AsInt(row.Cells["alvo"].Value, st.Target));
            st.Plus = Math.Max(0, AsInt(row.Cells["plus"].Value, st.Plus));
            st.Minus = Math.Max(0, AsInt(row.Cells["minus"].Value, st.Minus));
            int stock = AsInt(row.Cells["estoque"].Value);
            row.Cells["alvo"].Value = st.Target;
            row.Cells["plus"].Value = st.Plus;
            row.Cells["minus"].Value = st.Minus;
            row.Cells["res"].Value = Math.Max(0, stock + st.Plus - st.Minus);
            UpdateSummary();
            grid.InvalidateRow(e.RowIndex);
        }

        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var col = grid.Columns[e.ColumnIndex].Name;
            if (!col.StartsWith("b")) return;
            var row = grid.Rows[e.RowIndex];
            int id = AsInt(row.Cells["id"].Value);
            var st = GetState(id);
            int stock = AsInt(row.Cells["estoque"].Value);

            switch (col)
            {
                case "bPlus1": st.Plus += 1; break;
                case "bPlus5": st.Plus += 5; break;
                case "bPlus10": st.Plus += 10; break;
                case "bMinus1": st.Minus += 1; break;
                case "bMinus5": st.Minus += 5; break;
                case "bToTarget": st.Plus = Math.Max(st.Plus, Math.Max(0, st.Target - stock)); break;
                case "bKeypad":
                    using (var pad = new KeypadDialog(st.Plus, st.Minus))
                    {
                        if (pad.ShowDialog(this) == DialogResult.OK)
                        {
                            st.Plus = Math.Max(0, pad.Plus);
                            st.Minus = Math.Max(0, pad.Minus);
                        }
                    }
                    break;
            }
            row.Cells["plus"].Value = st.Plus;
            row.Cells["minus"].Value = st.Minus;
            row.Cells["res"].Value = Math.Max(0, stock + st.Plus - st.Minus);
            UpdateSummary();
            grid.InvalidateRow(e.RowIndex);
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            int res = AsInt(row.Cells["res"].Value);
            int target = AsInt(row.Cells["alvo"].Value);
            Color bg;
            if (res <= _critical) bg = Color.FromArgb(60, 0, 0);
            else if (res < target) bg = Color.FromArgb(60, 50, 0);
            else bg = Color.FromArgb(0, 50, 20);
            row.DefaultCellStyle.BackColor = bg;
            row.DefaultCellStyle.SelectionBackColor = ControlPaint.Light(bg);
        }

        private void SuggestToTarget()
        {
            int defTarget = (int)upDefaultTarget.Value;
            foreach (DataGridViewRow r in grid.Rows)
            {
                int id = AsInt(r.Cells["id"].Value);
                int stock = AsInt(r.Cells["estoque"].Value);
                var st = GetState(id);
                if (st.Target <= 0) st.Target = defTarget;
                int need = Math.Max(0, st.Target - stock);
                st.Plus = Math.Max(st.Plus, need);
                r.Cells["alvo"].Value = st.Target;
                r.Cells["plus"].Value = st.Plus;
                r.Cells["res"].Value = Math.Max(0, stock + st.Plus - st.Minus);
            }
            UpdateSummary();
            grid.Invalidate();
        }

        private void ImportCsv()
        {
            using var ofd = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|Todos (*.*)|*.*" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            var lines = File.ReadAllLines(ofd.FileName);
            int count = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new[] { ';', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var key = parts[0].Trim();
                if (!int.TryParse(parts[1].Trim(), out int qty)) qty = 0;
                if (qty <= 0) continue;
                Product? p = null;
                if (int.TryParse(key, out int id)) p = DataStore.Products.FirstOrDefault(x => x.Id == id);
                if (p == null) p = DataStore.Products.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (p == null) continue;
                var st = GetState(p.Id);
                st.Plus += qty;
                count++;
            }
            if (count > 0) { RebuildGrid(); UpdateSummary(); }
            MessageBox.Show(count > 0 ? $"Importado {count} linha(s)." : "Nenhuma linha válida.");
        }

        private void Confirm()
        {
            Result.Clear();
            foreach (var kv in edits)
            {
                if (kv.Value.Plus > 0 || kv.Value.Minus > 0)
                    Result.Add(new RestockLine { ProductId = kv.Key, Recebido = kv.Value.Plus, AjusteNegativo = kv.Value.Minus });
            }
            DialogResult = DialogResult.OK;
        }

        private int AsInt(object? v, int fallback = 0)
        {
            if (v == null) return fallback;
            return int.TryParse(v.ToString(), out var n) ? n : fallback;
        }

        private EditState GetState(int id)
        {
            if (!edits.TryGetValue(id, out var st))
            {
                st = new EditState { Plus = 0, Minus = 0, Target = (int)upDefaultTarget.Value };
                edits[id] = st;
            }
            return st;
        }

        private void UpdateSummary()
        {
            int items = edits.Count(kv => kv.Value.Plus > 0 || kv.Value.Minus > 0);
            int plus = edits.Sum(kv => kv.Value.Plus);
            int minus = edits.Sum(kv => kv.Value.Minus);
            lblSummary.Text = $"{items} item(s) • +{plus} • -{minus}";
        }
    }

    internal class KeypadDialog : Form
    {
        private NumericUpDown upPlus = new();
        private NumericUpDown upMinus = new();
        private Button okBtn = new(), cancelBtn = new();
        public int Plus => (int)upPlus.Value;
        public int Minus => (int)upMinus.Value;

        public KeypadDialog(int currentPlus, int currentMinus)
        {
            Text = "Ajustar quantidades";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            Width = 360; Height = 220;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var row1 = new FlowLayoutPanel { AutoSize = true };
            row1.Controls.Add(new Label { Text = "Recebido (+):", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            upPlus = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = Math.Max(0, currentPlus), Width = 120 };
            row1.Controls.Add(upPlus);

            var row2 = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            row2.Controls.Add(new Label { Text = "Ajuste (-):", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            upMinus = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = Math.Max(0, currentMinus), Width = 120 };
            row2.Controls.Add(upMinus);

            var actions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            okBtn = new Button { Text = "OK", AutoSize = true };
            okBtn.Click += (_, __) => DialogResult = DialogResult.OK;
            cancelBtn = new Button { Text = "Cancelar", AutoSize = true };
            cancelBtn.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(okBtn); actions.Controls.Add(cancelBtn);

            root.Controls.Add(row1); root.Controls.Add(row2); root.Controls.Add(actions);
        }
    }

    internal class ModernPrintPreviewDialog : Form
    {
        public ModernPrintPreviewDialog(Comanda comanda)
        {
            Text = $"Recibo - Mesa {comanda.TableName}";
            Size = new Size(450, 720);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(15, 23, 42); 
            ForeColor = Color.Gainsboro;

            var paperPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 41, 59),
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            paperPanel.Controls.Add(MkLabel("MVP RESTAURANTE", 14, FontStyle.Bold, Color.White, true));
            paperPanel.Controls.Add(MkLabel("Comprovante de Venda", 10, FontStyle.Regular, Color.Gray, true));
            paperPanel.Controls.Add(MkDivider());
            
            paperPanel.Controls.Add(MkRow("Comanda:", $"#{comanda.Id}"));
            paperPanel.Controls.Add(MkRow("Mesa/Cliente:", comanda.TableName));
            paperPanel.Controls.Add(MkRow("Data:", comanda.CreatedAt.ToString("dd/MM/yyyy HH:mm")));
            paperPanel.Controls.Add(MkDivider());

            var headerItem = new TableLayoutPanel { Width = 380, Height = 25, Margin = new Padding(0) };
            headerItem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            headerItem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            headerItem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            
            headerItem.Controls.Add(new Label { Text = "ITEM", ForeColor = Color.Gray, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            headerItem.Controls.Add(new Label { Text = "QTD", ForeColor = Color.Gray, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
            headerItem.Controls.Add(new Label { Text = "TOTAL", ForeColor = Color.Gray, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, 0);
            paperPanel.Controls.Add(headerItem);
            
            foreach (var item in comanda.Items)
            {
                var row = new TableLayoutPanel { Width = 380, Height = 25, Margin = new Padding(0, 2, 0, 2) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

                row.Controls.Add(new Label { Text = item.Name, ForeColor = Color.White, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                row.Controls.Add(new Label { Text = $"{item.Qty}x", ForeColor = Color.LightGray, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
                row.Controls.Add(new Label { Text = (item.Qty * item.Price).ToString("C"), ForeColor = Color.White, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, 0);
                
                paperPanel.Controls.Add(row);
            }
            paperPanel.Controls.Add(MkDivider());

            var totals = comanda.Totals;
            paperPanel.Controls.Add(MkRow("Subtotal", totals.Subtotal.ToString("C")));
            
            if (totals.Service > 0)
                paperPanel.Controls.Add(MkRow($"Serviço ({comanda.ServicePct}%)", totals.Service.ToString("C")));
            
            if (totals.Discounts > 0)
                paperPanel.Controls.Add(MkRow("Descontos", "-" + totals.Discounts.ToString("C"), Color.FromArgb(239, 68, 68))); 

            paperPanel.Controls.Add(MkDivider());

            var pnlTotal = MkRow("TOTAL FINAL", totals.Total.ToString("C"), Color.FromArgb(34, 197, 94), 14, FontStyle.Bold);
            pnlTotal.Padding = new Padding(0, 5, 0, 5); 
            paperPanel.Controls.Add(pnlTotal);

            if (totals.Paid > 0)
            {
                paperPanel.Controls.Add(MkDivider());
                paperPanel.Controls.Add(MkLabel("Pagamentos Realizados:", 9, FontStyle.Bold, Color.Gray));
                foreach(var p in comanda.Payments)
                {
                    paperPanel.Controls.Add(MkRow($"- {p.Method}", p.Amount.ToString("C"), Color.LightSkyBlue));
                }
                paperPanel.Controls.Add(new Panel { Height = 10 }); 
                
                if (totals.Remaining > 0)
                    paperPanel.Controls.Add(MkRow("FALTA PAGAR", totals.Remaining.ToString("C"), Color.Orange, 11, FontStyle.Bold));
                else
                    paperPanel.Controls.Add(MkLabel("✔ CONTA QUITADA", 11, FontStyle.Bold, Color.FromArgb(34, 197, 94), true));
            }

            paperPanel.Controls.Add(new Panel { Height = 20 });
            paperPanel.Controls.Add(MkLabel("Obrigado pela preferência!", 10, FontStyle.Italic, Color.Gray, true));

            var pnlBottom = new FlowLayoutPanel {
                Dock = DockStyle.Bottom, Height = 80,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(15),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var btnPrint = new Button {
                Text = "🖨️ Imprimir", DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.Black,
                Height = 45, Width = 140, Margin = new Padding(10,0,0,0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnPrint.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button {
                Text = "Fechar", DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White,
                Height = 45, Width = 100,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            pnlBottom.Controls.Add(btnPrint);
            pnlBottom.Controls.Add(btnCancel);

            Controls.Add(paperPanel);
            Controls.Add(pnlBottom);
        }

        private Panel MkRow(string label, string value, Color? valColor = null, float size = 10, FontStyle style = FontStyle.Regular)
        {
            var p = new TableLayoutPanel { Width = 380, Height = 28, Margin = new Padding(0, 1, 0, 1) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var l1 = new Label { 
                Text = label, 
                ForeColor = Color.Gainsboro, 
                Font = new Font("Segoe UI", size, style), 
                AutoSize = false, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleLeft 
            };
            
            var l2 = new Label { 
                Text = value, 
                ForeColor = valColor ?? Color.White, 
                Font = new Font("Segoe UI", size, style), 
                AutoSize = false, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleRight 
            };

            p.Controls.Add(l1, 0, 0);
            p.Controls.Add(l2, 1, 0);
            return p;
        }

        private Label MkLabel(string text, float size, FontStyle style, Color color, bool center = false)
        {
            var l = new Label { 
                Text = text, 
                Font = new Font("Segoe UI", size, style), 
                ForeColor = color, 
                AutoSize = true, 
                Margin = new Padding(0, 2, 0, 5)
            };
            if (center) { l.AutoSize = false; l.Width = 380; l.TextAlign = ContentAlignment.MiddleCenter; }
            return l;
        }

        private Panel MkDivider()
        {
            return new Panel { Height = 1, Width = 380, BackColor = Color.FromArgb(60, 70, 90), Margin = new Padding(0, 10, 0, 10) };
        }
    }
       // === CLASSE DE HISTÓRICO (DEVE SER UMA CLASSE À PARTE, FORA DA MAINFORM) ===
    internal class ModernHistoryDialog : Form
    {
        public ModernHistoryDialog(List<Comanda> historyList)
        {
            Text = "Histórico de Vendas";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            BackColor = Color.FromArgb(15, 23, 42); 
            ForeColor = Color.Gainsboro;

            var lblTitle = new Label {
                Text = $"Histórico ({historyList.Count} registros)",
                Dock = DockStyle.Top, Height = 60, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.White, Padding = new Padding(15, 0, 0, 0)
            };

            var grid = new DataGridView {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            grid.Columns.Add("id", "ID");
            grid.Columns.Add("mesa", "Mesa / Cliente");
            grid.Columns.Add("data", "Fechamento");
            grid.Columns.Add("total", "Total R$");
            grid.Columns.Add("fopag", "FOPAG");
            
            decimal somaTotal = 0;
            foreach (var c in historyList) {
                // CORREÇÃO CS0023: Removido o '?' desnecessário em Totals
                decimal valorComanda = c.Totals.Total; 
                if (c.Status == "canceled") valorComanda = 0;
                somaTotal += valorComanda;

                grid.Rows.Add(
                    "#" + c.Id, 
                    c.TableName, 
                    c.ClosedAt?.ToString("dd/MM HH:mm"), 
                    valorComanda.ToString("C"), 
                    c.Status == "canceled" ? "CANCELADA" : "PAGO"
                );
            }

            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(15) };
            var btnClose = new Button { Text = "Fechar", DialogResult = DialogResult.OK, Height = 40, Width = 120, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White };
            var lblTotalGeral = new Label { Text = $"TOTAL: {somaTotal:C}", AutoSize = true, ForeColor = Color.FromArgb(34, 197, 94), Font = new Font("Segoe UI", 16, FontStyle.Bold) };

            pnlBottom.Controls.Add(btnClose); pnlBottom.Controls.Add(lblTotalGeral); 
            Controls.Add(grid); Controls.Add(lblTitle); Controls.Add(pnlBottom);
            grid.BringToFront();
        }
    }
    
}
// CLASSE AUXILIAR TOTALMENTE EXTERNA
public class RoundedPanel : Panel
{
    public int Radius { get; set; } = 30;
    public Color CorDeFundo { get; set; } = Color.FromArgb(30, 30, 35);
    public RoundedPanel() { this.BackColor = Color.Transparent; this.DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = new System.Drawing.Drawing2D.GraphicsPath()) {
            var r = new Rectangle(0, 0, this.Width, this.Height);
            int d = Radius;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
            path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
            path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
            path.CloseFigure();
            using (var brush = new SolidBrush(CorDeFundo)) { e.Graphics.FillPath(brush, path); }
        }
    }
}

}
