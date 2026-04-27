using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using RestauranteMVP.Core;

namespace RestauranteMVP.UI
{
    public class RestockDialog : Form
    {
        private readonly List<Product> _items;
        private readonly int _threshold;

        private DataGridView grid = new();
        private Button okBtn = new();
        private Button cancelBtn = new();

        // Resultado: (ProductId, AddQty)
        public List<(int ProductId, int AddQty)> Result { get; private set; } = new();

        public RestockDialog(List<Product> criticalItems, int threshold)
        {
            _items = criticalItems;
            _threshold = threshold;

            Text = "Reposição (Avançada)";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 1000; // Aumentei a largura para caber todas as colunas
            Height = 460;

            BuildUI();
            LoadRows();
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                ReadOnly = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false
            };

            // Estilo Dark Mode (mantido)
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold); 
            grid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 65, 81);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55); 

            // ------------------------------------------------------------------------------------------------
            // Definição das Colunas
            // ------------------------------------------------------------------------------------------------
            grid.Columns.Clear();

            // Colunas de Dados
            var colId = new DataGridViewTextBoxColumn { Name = "id", HeaderText = "Id", Visible = false };
            var colTipo = new DataGridViewTextBoxColumn { Name = "tipo", HeaderText = "Tipo", FillWeight = 40, ReadOnly = true };
            var colNome = new DataGridViewTextBoxColumn { Name = "nome", HeaderText = "Produto/Ingrediente", FillWeight = 150, ReadOnly = true };
            var colCat = new DataGridViewTextBoxColumn { Name = "categoria", HeaderText = "Categoria", FillWeight = 80, ReadOnly = true };
            var colEst = new DataGridViewTextBoxColumn { Name = "estoque", HeaderText = "Estoque", FillWeight = 50, ReadOnly = true };
            var colUn = new DataGridViewTextBoxColumn { Name = "un", HeaderText = "Un.", FillWeight = 40, ReadOnly = true };
            var colAlvo = new DataGridViewTextBoxColumn { Name = "alvo", HeaderText = "Alvo", FillWeight = 40, ReadOnly = true };
            var colRec = new DataGridViewTextBoxColumn { Name = "recebido", HeaderText = "Recebido", FillWeight = 60, ReadOnly = true };

            // Coluna de Ajuste (Onde o usuário digita)
            var colAjuste = new DataGridViewTextBoxColumn { Name = "ajuste", HeaderText = "Ajuste", FillWeight = 60, ReadOnly = false };

            // Coluna de Resultado (Estoque + Ajuste)
            var colResultado = new DataGridViewTextBoxColumn { Name = "resultado", HeaderText = "Resultado", FillWeight = 60, ReadOnly = true };

            // Coluna do botão "Até alvo" (DataGridViewButtonColumn)
            var colAteAlvo = new DataGridViewButtonColumn
            {
                Name = "atealvo",
                HeaderText = "Até alvo",
                Text = "Até alvo",
                UseColumnTextForButtonValue = true,
                FillWeight = 50
            };

            // Estilo do botão para se parecer com o tema escuro
            colAteAlvo.DefaultCellStyle.BackColor = Color.FromArgb(55, 65, 81); 
            colAteAlvo.DefaultCellStyle.ForeColor = Color.White;
            colAteAlvo.CellTemplate.Style.Padding = new Padding(3, 1, 3, 1); 
            colAteAlvo.FlatStyle = FlatStyle.Popup; // Ajuda a parecer mais um botão

            // Coluna 'Teclado...'
            var colTeclado = new DataGridViewTextBoxColumn { Name = "teclado", HeaderText = "Teclado...", FillWeight = 60, ReadOnly = true };


            grid.Columns.AddRange(
                colId, colTipo, colNome, colCat, colEst, colUn, 
                colAlvo, colRec, colAjuste, colResultado, 
                colAteAlvo, colTeclado
            );
            
            // Adiciona os manipuladores de eventos essenciais
            grid.CellContentClick += Grid_CellContentClick;
            grid.CellValueChanged += Grid_CellValueChanged;
            grid.DataError += Grid_DataError; // Para tratar erros de conversão de tipo (ex: usuário digita texto no 'Ajuste')

            // ------------------------------------------------------------------------------------------------
            // Configuração dos Botões de Ação Inferiores
            // ------------------------------------------------------------------------------------------------
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(8)
            };
            okBtn = new Button { Text = "Aplicar", AutoSize = true };
            okBtn.Click += (_, __) => Confirm();
            cancelBtn = new Button { Text = "Cancelar", AutoSize = true };
            cancelBtn.Click += (_, __) => DialogResult = DialogResult.Cancel;

            actions.Controls.Add(okBtn);
            actions.Controls.Add(cancelBtn);

            root.Controls.Add(grid, 0, 0);
            root.Controls.Add(actions, 0, 1);
        }
        
        // NOVO MÉTODO - Lógica de clique do botão "Até alvo"
        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Verifica se o clique foi em uma célula válida (não no cabeçalho)
            if (e.RowIndex < 0) return;

            // Obtém o nome da coluna que foi clicada
            string clickedColumnName = grid.Columns[e.ColumnIndex].Name;

            // 1. Verifica se a coluna clicada é a "atealvo"
            if (clickedColumnName.Equals("atealvo", StringComparison.OrdinalIgnoreCase))
            {
                DataGridViewRow row = grid.Rows[e.RowIndex];

                // Pega o Estoque Atual e o Alvo
                if (!int.TryParse(row.Cells["estoque"].Value?.ToString(), out int estoqueAtual))
                    estoqueAtual = 0;

                if (!int.TryParse(row.Cells["alvo"].Value?.ToString(), out int alvo))
                    alvo = _threshold;

                // 2. Calcula a quantidade que falta para atingir o alvo
                int quantidadeParaAlvo = Math.Max(0, alvo - estoqueAtual);

                // 3. Aplica essa quantidade na coluna "Ajuste"
                row.Cells["ajuste"].Value = quantidadeParaAlvo;

                // Não é necessário recalcular o Resultado aqui, o CellValueChanged fará isso.
                // Apenas garante que a mudança de valor na célula 'ajuste' será processada imediatamente.
                grid.EndEdit();
            }
        }
        
        // NOVO MÉTODO - Lógica para atualizar a coluna "Resultado" ao alterar "Ajuste"
        private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // Verifica se a célula alterada é a coluna "ajuste"
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name.Equals("ajuste", StringComparison.OrdinalIgnoreCase))
            {
                DataGridViewRow row = grid.Rows[e.RowIndex];
                
                // Tenta obter o Estoque Atual
                if (!int.TryParse(row.Cells["estoque"].Value?.ToString(), out int estoqueAtual))
                    estoqueAtual = 0;

                // Tenta obter o novo Ajuste
                // Usamos Convert.ToInt32 em vez de TryParse para garantir que o DataError seja pego se a entrada for inválida
                int ajuste;
                if (!int.TryParse(row.Cells["ajuste"].Value?.ToString(), out ajuste))
                {
                    ajuste = 0;
                    // Se a entrada for inválida, não atualizamos para não forçar um erro. O DataError lida com isso.
                }

                // Calcula e atualiza o Resultado
                row.Cells["resultado"].Value = estoqueAtual + ajuste;
            }
        }
        
        // NOVO MÉTODO - Manipulador para tratar erros de dados (ex: digita texto em coluna numérica)
        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Para colunas de números (como 'ajuste'), podemos suprimir o erro padrão
            // e forçar o valor para 0, para evitar a mensagem de erro feia do DataGridView.
            if (grid.Columns[e.ColumnIndex].Name.Equals("ajuste", StringComparison.OrdinalIgnoreCase))
            {
                // Define o valor da célula inválida como 0 e notifica que o erro foi tratado.
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = 0;
                e.Cancel = false;
            }
        }


        private void LoadRows()
        {
            grid.Rows.Clear();

            foreach (var p in _items)
            {
                var tipo = "Produto";
                if ((p.Category ?? "").Trim().Equals("Ingredientes", StringComparison.OrdinalIgnoreCase))
                    tipo = "Ingrediente";

                int ajuste = 0; 
                int resultado = p.Stock + ajuste;

                grid.Rows.Add(
                    p.Id,
                    tipo,
                    p.Name,
                    p.Category,  // Categoria
                    p.Stock,
                    p.Unit,
                    _threshold,  // Alvo
                    0,           // Recebido (inicial 0)
                    ajuste,      // Ajuste (inicial 0)
                    resultado,   // Resultado (Estoque + Ajuste)
                    "Até alvo",  // Texto do botão
                    ""           // Teclado...
                );

                // Aplica cor de destaque para as linhas de Produto (verde escuro, como na imagem)
                if (tipo == "Produto")
                {
                    grid.Rows[grid.Rows.Count - 1].DefaultCellStyle.BackColor = Color.FromArgb(0, 70, 0); 
                    // Garante que a linha alternada seja um tom diferente
                    grid.Rows[grid.Rows.Count - 1].Cells[0].Style.BackColor = Color.FromArgb(0, 70, 0); 
                }
            }
        }

        private void Confirm()
        {
            Result.Clear();

            // Certifica-se de que qualquer edição pendente seja finalizada antes de ler os valores
            if (grid.IsCurrentCellInEditMode)
            {
                grid.EndEdit();
            }

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;

                int id = Convert.ToInt32(row.Cells["id"].Value);
                
                // Pega o valor da coluna "ajuste"
                var addStr = row.Cells["ajuste"].Value?.ToString() ?? "0"; 
                if (!int.TryParse(addStr, out int add)) add = 0;
                
                // Garante que o valor adicionado não é negativo
                if (add < 0) add = 0;

                if (add > 0)
                    Result.Add((id, add));
            }

            DialogResult = DialogResult.OK;
        }
    }
}