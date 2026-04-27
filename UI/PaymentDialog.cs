#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RestauranteMVP.Core;
using RestauranteMVP.Data;

namespace RestauranteMVP.UI
{
    public class PaymentDialog : Form
    {
        private readonly Comanda _comanda;
        
        // Componentes Visuais
        private DataGridView gridItems;
        private Label lblTotal, lblPaid, lblRemaining;
        
        // Inputs Pagamento
        private NumericUpDown numValorAbater;   
        private NumericUpDown numValorRecebido; 
        private ComboBox comboMethod;
        private Button btnConfirmPayment;
        
        // Botões de Ação
        private Button btnSplitItem;
        private Button btnUndoSplit;
        private Button btnPaySelection; 
        private Button btnSelectAll; // NOVO BOTÃO
        private Button btnPayAll; 
        private CheckBox chkServiceFee; 

        // Lista de itens no "carrinho" do pagamento
        private List<PaymentScopeItem> _scopeItems = new(); 
        private bool _isFullPaymentMode = false; 

        public bool PaymentMade { get; private set; } = false;

        public PaymentDialog(Comanda c)
        {
            _comanda = c;
            Text = $"Caixa - Mesa {c.TableName}";
            Size = new Size(1280, 760); 
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(15, 23, 42); 
            ForeColor = Color.FromArgb(226, 232, 240); 
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            this.KeyPreview = true; 

            BuildLayout();
            UpdateTotals();

            // Atalho CTRL + A para selecionar tudo
            this.KeyDown += (s, e) => {
                if (e.Control && e.KeyCode == Keys.A) {
                    SelectAllRowsAction();
                    e.SuppressKeyPress = true;
                }
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                if (!numValorAbater.Focused && !numValorRecebido.Focused && gridItems.Focused)
                {
                    ToggleCurrentRowSelection();
                    return true; 
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BuildLayout()
        {
            var mainSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0) };
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); 
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); 
            Controls.Add(mainSplit);

            // === ESQUERDA ===
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            var lblTitle = new Label { Text = "📦 Itens da Comanda", Font = new Font("Segoe UI", 16, FontStyle.Bold), Dock = DockStyle.Top, Height = 40, ForeColor = Color.White };
            
            gridItems = new DataGridView { 
                Dock = DockStyle.Fill, 
                BackgroundColor = Color.FromArgb(30, 41, 59), 
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, 
                GridColor = Color.FromArgb(51, 65, 85),
                ReadOnly = false, 
                EditMode = DataGridViewEditMode.EditOnEnter 
            };

            gridItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            gridItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            gridItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            gridItems.ColumnHeadersHeight = 40;
            gridItems.DefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            gridItems.DefaultCellStyle.ForeColor = Color.White;
            gridItems.RowTemplate.Height = 45;

            gridItems.Columns.Add(new DataGridViewCheckBoxColumn { Name = "select", HeaderText = "", Width = 30 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "PRODUTO", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            
            var colPayQty = new DataGridViewTextBoxColumn { Name = "payQty", HeaderText = "QTD PAGAR", Width = 90 };
            colPayQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colPayQty.DefaultCellStyle.BackColor = Color.FromArgb(40, 55, 75); 
            colPayQty.DefaultCellStyle.ForeColor = Color.Yellow;
            colPayQty.DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            gridItems.Columns.Add(colPayQty);

            gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "totalQty", HeaderText = "RESTANTE", Width = 80, ReadOnly = true });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "price", HeaderText = "VALOR UN.", Width = 100, ReadOnly = true });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "SITUAÇÃO", Width = 130, ReadOnly = true });

            gridItems.CellContentClick += GridItems_CellContentClick; 
            gridItems.CellEndEdit += GridItems_CellEndEdit; 

            LoadGridItems();
            
            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10) };
            gridContainer.Controls.Add(gridItems);

            // PAINEL DE AÇÕES
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.LeftToRight };
            
            chkServiceFee = new CheckBox { Text = "+10%", Checked = true, ForeColor = Color.Orange, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 12, 10, 0), Cursor = Cursors.Hand };
            
            btnPaySelection = CreateActionButton("Pagar Selecionados", Color.Teal, Color.White);
            btnPaySelection.Click += BtnPaySelection_Click;

            // NOVO BOTÃO SELECIONAR TUDO
            btnSelectAll = CreateActionButton("Selecionar Tudo", Color.FromArgb(51, 65, 85), Color.White);
            btnSelectAll.Click += (s, e) => SelectAllRowsAction();

            btnSplitItem = CreateActionButton("Dividir", Color.FromArgb(234, 179, 8), Color.Black);
            btnSplitItem.Click += BtnSplitItem_Click;

            btnUndoSplit = CreateActionButton("Desfazer", Color.FromArgb(220, 38, 38), Color.White);
            btnUndoSplit.Click += BtnUndoSplit_Click;

            actionPanel.Controls.Add(chkServiceFee);
            actionPanel.Controls.Add(btnSelectAll); // Adicionado aqui
            actionPanel.Controls.Add(btnPaySelection); 
            actionPanel.Controls.Add(btnSplitItem); 
            actionPanel.Controls.Add(btnUndoSplit);

            leftPanel.Controls.Add(gridContainer); leftPanel.Controls.Add(actionPanel); leftPanel.Controls.Add(lblTitle);
            mainSplit.Controls.Add(leftPanel, 0, 0);

            // === DIREITA ===
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 15, 25), Padding = new Padding(25) };
            var flowRight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            flowRight.Resize += (s, e) => { foreach(Control c in flowRight.Controls) c.Width = flowRight.Width - 10; }; 

            lblTotal = CreateBigLabel("TOTAL DA COMANDA", _comanda.Totals.Total, Color.White);
            lblPaid = CreateBigLabel("JÁ PAGO", _comanda.Totals.Paid, Color.FromArgb(74, 222, 128));
            lblRemaining = CreateBigLabel("RESTANTE A PAGAR", _comanda.Totals.Remaining, Color.FromArgb(248, 113, 113));
            
            btnPayAll = new Button { 
                Text = "💰 PAGAR CONTA TODA", Height = 55, 
                BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand, Margin = new Padding(0, 10, 0, 20)
            };
            btnPayAll.Click += BtnPayAll_Click;

            numValorAbater = CreateStyledNumeric();
            numValorRecebido = CreateStyledNumeric();
            numValorAbater.ValueChanged += (_, __) => numValorRecebido.Value = numValorAbater.Value;

            comboMethod = new ComboBox { Height = 40, Font = new Font("Segoe UI", 12), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.White };
            comboMethod.Items.AddRange(new object[] { "Dinheiro", "Pix", "Cartão Crédito", "Cartão Débito", "Vale Refeição" });
            comboMethod.SelectedIndex = 0;

            btnConfirmPayment = new Button { 
                Text = "CONFIRMAR PAGAMENTO", Height = 60, 
                BackColor = Color.FromArgb(34, 197, 94), ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand, Margin = new Padding(0, 30, 0, 0)
            };
            btnConfirmPayment.Click += ConfirmPayment_Click;

            flowRight.Controls.Add(lblTotal); flowRight.Controls.Add(lblPaid); flowRight.Controls.Add(lblRemaining);
            flowRight.Controls.Add(btnPayAll);
            flowRight.Controls.Add(new Label { Text = "VALOR A PAGAR AGORA", ForeColor = Color.Gray, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true });
            flowRight.Controls.Add(numValorAbater);
            flowRight.Controls.Add(new Label { Text = "DINHEIRO RECEBIDO", ForeColor = Color.Gray, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true });
            flowRight.Controls.Add(numValorRecebido);
            flowRight.Controls.Add(new Label { Text = "FORMA DE PAGAMENTO", ForeColor = Color.Gray, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true });
            flowRight.Controls.Add(comboMethod);
            flowRight.Controls.Add(btnConfirmPayment);

            rightPanel.Controls.Add(flowRight);
            mainSplit.Controls.Add(rightPanel, 1, 0);
        }

        // FUNÇÃO PARA SELECIONAR TUDO RAPIDAMENTE
        private void SelectAllRowsAction()
        {
            foreach (DataGridViewRow row in gridItems.Rows)
            {
                if (!row.ReadOnly) 
                {
                    row.Cells["select"].Value = true;
                    var data = (RowData)row.Tag;
                    row.Cells["payQty"].Value = data.MaxQty; // Preenche a qtd máxima
                }
            }
            gridItems.EndEdit();
            BtnPaySelection_Click(this, EventArgs.Empty); // Já dispara o cálculo do valor total
        }

        private void LoadGridItems()
        {
            gridItems.Rows.Clear();
            foreach (var item in _comanda.Items)
            {
                string status = "Aberto";
                Color statusColor = Color.White;
                string priceDisplay = item.Price.ToString("N2");
                bool isFullyPaid = false;
                int maxQty = 0; 

                if (item.QtyPaid >= item.Qty) { status = "✅ PAGO"; statusColor = Color.LimeGreen; isFullyPaid = true; }
                else if (item.SplitTotalParts > 0) {
                    int partsRemaining = item.SplitTotalParts - item.SplitPaidParts;
                    if (partsRemaining <= 0) { status = "✅ PAGO"; isFullyPaid = true; }
                    else {
                        status = $"⏳ Dividido: {item.SplitPaidParts}/{item.SplitTotalParts}"; statusColor = Color.Cyan;
                        priceDisplay = $"{(item.Price / item.SplitTotalParts):N2} (Fatia)";
                        maxQty = partsRemaining;
                    }
                }
                else {
                    maxQty = item.Qty - item.QtyPaid;
                    if (maxQty == 0) { status = "✅ PAGO"; isFullyPaid = true; }
                    else if (item.QtyPaid > 0) { status = $"Pago {item.QtyPaid}/{item.Qty}"; statusColor = Color.Yellow; }
                }

                int idx = gridItems.Rows.Add(false, item.Name, maxQty, maxQty, priceDisplay, status);
                gridItems.Rows[idx].Tag = new RowData { Item = item, MaxQty = maxQty }; 
                gridItems.Rows[idx].Cells["status"].Style.ForeColor = statusColor;

                if (isFullyPaid) {
                    gridItems.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
                    gridItems.Rows[idx].ReadOnly = true; 
                    gridItems.Rows[idx].Cells["payQty"].Value = ""; 
                }
            }
        }

        private void GridItems_CellContentClick(object? sender, DataGridViewCellEventArgs e) { 
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) {
                gridItems.CommitEdit(DataGridViewDataErrorContexts.Commit); 
                _isFullPaymentMode = false;
            }
        }

        private void GridItems_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && gridItems.Columns[e.ColumnIndex].Name == "payQty")
            {
                var row = gridItems.Rows[e.RowIndex];
                var data = (RowData)row.Tag;
                if (int.TryParse(row.Cells["payQty"].Value?.ToString(), out int val))
                {
                    if (val < 0) row.Cells["payQty"].Value = 0;
                    if (val > data.MaxQty) row.Cells["payQty"].Value = data.MaxQty;
                }
                row.Cells["select"].Value = Convert.ToInt32(row.Cells["payQty"].Value) > 0;
                _isFullPaymentMode = false;
            }
        }
        
        private void ToggleCurrentRowSelection() {
            if (gridItems.CurrentRow == null || gridItems.CurrentRow.ReadOnly) return;
            var cell = (DataGridViewCheckBoxCell)gridItems.CurrentRow.Cells["select"];
            cell.Value = !Convert.ToBoolean(cell.Value);
            _isFullPaymentMode = false;
        }

        private void BtnPayAll_Click(object? sender, EventArgs e) => SelectAllAndCalc();

        private void SelectAllAndCalc()
        {
            _isFullPaymentMode = true; 
            foreach(DataGridViewRow row in gridItems.Rows)
                if (!row.ReadOnly) row.Cells["select"].Value = true;

            decimal totalRestante = _comanda.Totals.Remaining;
            if (chkServiceFee.Checked) totalRestante *= 1.10m;
            SetPaymentValues(totalRestante);
            gridItems.Focus();
        }

        private void BtnSplitItem_Click(object? sender, EventArgs e)
        {
            var row = gridItems.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Convert.ToBoolean(r.Cells["select"].Value));
            if (row == null) { MessageBox.Show("Selecione UM item."); return; }
            var item = ((RowData)row.Tag).Item;
            if (item.Qty > 1 && MessageBox.Show("Separar 1 unidade?", "Dividir", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                item.Qty--;
                var newItem = new ComandaItem { ProductId = item.ProductId, Name = item.Name, Price = item.Price, Qty = 1 };
                _comanda.Items.Add(newItem);
                item = newItem; 
            }
            string input = ShowInputDialog("Dividir em quantas partes?", "Dividir");
            if (int.TryParse(input, out int parts) && parts > 1) {
                item.SplitTotalParts = parts; LoadGridItems();
            }
        }

        private void BtnUndoSplit_Click(object? sender, EventArgs e)
        {
            var row = gridItems.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => Convert.ToBoolean(r.Cells["select"].Value));
            if (row == null) return;
            var item = ((RowData)row.Tag).Item;
            if (item.SplitPaidParts == 0) { item.SplitTotalParts = 0; LoadGridItems(); }
        }

        private void BtnPaySelection_Click(object? sender, EventArgs e)
        {
            PrepareScopeItems();
            if (_scopeItems.Count == 0) return;
            decimal somaTotal = _scopeItems.Sum(x => x.ChunkValue);
            if (chkServiceFee.Checked) somaTotal *= 1.10m;
            SetPaymentValues(somaTotal);
            gridItems.Focus();
        }

        private void PrepareScopeItems()
        {
            _scopeItems.Clear();
            foreach (DataGridViewRow row in gridItems.Rows) {
                if (!Convert.ToBoolean(row.Cells["select"].Value)) continue;
                var item = ((RowData)row.Tag).Item;
                if (!int.TryParse(row.Cells["payQty"].Value?.ToString(), out int qtd) || qtd <= 0) continue;
                decimal chunk = item.SplitTotalParts > 0 ? (item.Price / item.SplitTotalParts) : item.Price;
                for(int i=0; i<qtd; i++) _scopeItems.Add(new PaymentScopeItem { Item = item, IsSplitPayment = item.SplitTotalParts > 0, ChunkValue = chunk });
            }
        }

        private void ConfirmPayment_Click(object? sender, EventArgs e)
        {
            if (_scopeItems.Count == 0) PrepareScopeItems();
            if (_scopeItems.Count == 0) { MessageBox.Show("Selecione os itens."); return; }

            decimal aPagar = numValorAbater.Value;
            if (numValorRecebido.Value < aPagar) { MessageBox.Show("Dinheiro insuficiente."); return; }
            
            foreach (var scope in _scopeItems) {
                if (scope.IsSplitPayment) scope.Item.SplitPaidParts++;
                else scope.Item.QtyPaid++; 
            }

            _comanda.Payments.Add(new Payment { Amount = aPagar, Method = comboMethod.SelectedItem.ToString(), Timestamp = DateTime.Now });
            PaymentMade = true;
            decimal troco = numValorRecebido.Value - aPagar;
            UpdateTotals(); LoadGridItems(); 
            if (troco > 0) ShowChangeDialog(numValorRecebido.Value, aPagar, troco);
            if (_comanda.Totals.Remaining <= 0.05m) DialogResult = DialogResult.OK;
        }

        private void UpdateTotals() {
            decimal paid = _comanda.Payments.Sum(p => p.Amount);
            var t = _comanda.Totals; t.Paid = paid; t.Remaining = Math.Max(0, t.Total - paid); _comanda.Totals = t;
            lblPaid.Text = $"{t.Paid:C}\nJÁ PAGO"; lblRemaining.Text = $"{t.Remaining:C}\nRESTANTE";
        }

        private void SetPaymentValues(decimal val) { numValorAbater.Maximum = 100000; numValorAbater.Value = val; }
        private Label CreateBigLabel(string t, decimal v, Color c) => new Label { Text = $"{v:C}\n{t}", AutoSize = true, ForeColor = c, Font = new Font("Segoe UI", 14, FontStyle.Bold), Margin = new Padding(0, 0, 0, 15) };
        private NumericUpDown CreateStyledNumeric() => new NumericUpDown { DecimalPlaces = 2, Maximum = 99999, Font = new Font("Segoe UI", 16), Height = 40, Width = 250 };
        private Button CreateActionButton(string t, Color bg, Color fg) => new Button { Text = t, AutoSize = true, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Height = 45, Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand };
        private string ShowInputDialog(string t, string c) {
            Form p = new Form() { Width = 400, Height = 200, Text = c, StartPosition = FormStartPosition.CenterScreen };
            TextBox tx = new TextBox() { Left = 20, Top = 80, Width = 340, Font = new Font("Segoe UI", 12) };
            Button b = new Button() { Text = "Ok", Left = 250, Width = 100, Top = 120, DialogResult = DialogResult.OK };
            p.Controls.Add(tx); p.Controls.Add(b); p.Controls.Add(new Label { Left = 20, Top = 20, Text = t, AutoSize = true });
            return p.ShowDialog() == DialogResult.OK ? tx.Text : "";
        }
        private void ShowChangeDialog(decimal r, decimal a, decimal t) { using (var dlg = new ModernChangeDialog(r, a, t)) dlg.ShowDialog(this); }
        private class PaymentScopeItem { public ComandaItem Item { get; set; } = null!; public bool IsSplitPayment { get; set; } public decimal ChunkValue { get; set; } }
        private class RowData { public ComandaItem Item { get; set; } = null!; public int MaxQty { get; set; } }
    }
    
    internal class ModernChangeDialog : Form {
        public ModernChangeDialog(decimal r, decimal a, decimal t) {
            Text = "Troco"; Size = new Size(400, 450); StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(15, 23, 42); ForeColor = Color.White;
            Controls.Add(new Label { Text = "TROCO A DEVOLVER", Dock = DockStyle.Top, Font = new Font("Segoe UI", 12), TextAlign = ContentAlignment.BottomCenter, Height = 50, ForeColor = Color.Orange });
            Controls.Add(new Label { Text = t.ToString("C"), Dock = DockStyle.Top, Font = new Font("Segoe UI", 36, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, Height = 100 });
            var b = new Button { Text = "OK (Entreguei)", Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(34, 197, 94), FlatStyle = FlatStyle.Flat, ForeColor = Color.Black, Font = new Font("Segoe UI", 12, FontStyle.Bold), DialogResult = DialogResult.OK };
            Controls.Add(b);
        }
    }
}