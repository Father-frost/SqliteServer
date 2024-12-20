using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Transactions;

namespace SqliteServer
{
	internal class Program
	{

		const int port = 8888;
		private static SqliteConnection? conn;

		private static String? dbFileName;
		private static SqliteConnection? m_dbConn;
		private static SqliteCommand? m_sqlCmd;

		static async Task Main(string[] args)
		{
			var server = new TcpListener(IPAddress.Any, port);
			try
			{
				// запуск слушателя
				server.Start();
				Console.WriteLine("Сервер запущен. Ожидание подключений... ");

				//conn = new SqliteConnection("Data source = marking.db");
				while (true)
				{
					using var tcpClient = await server.AcceptTcpClientAsync();
					// получаем объект NetworkStream для взаимодействия с клиентом
					Console.WriteLine("Клиент подключился к серверу.");
					while (tcpClient.Connected)
					{
						var stream = tcpClient.GetStream();

						byte[] cmd = new byte[256];
						StringBuilder builder = new StringBuilder();
						int bytes = 0;
						do
						{
							bytes = stream.Read(cmd, 0, cmd.Length);
							builder.Append(Encoding.UTF8.GetString(cmd, 0, bytes));
						}
						while (stream.DataAvailable);

						string message = builder.ToString();


						Console.WriteLine($"Получен код маркировки от клиента: {message}");
						m_dbConn = new SqliteConnection();
						dbFileName = "marking.db";


						string query = @"select kpr from MarkingTasks where Code = '" + message + @"' LIMIT 1";

						try
						{
							m_dbConn = new SqliteConnection("Data Source=" + dbFileName);
							m_dbConn.Open();
							m_sqlCmd = new SqliteCommand(query, m_dbConn);
							using (SqliteDataReader reader = m_sqlCmd.ExecuteReader())
							{
								if (reader.HasRows) // если есть данные
								{
									while (await reader.ReadAsync())   // построчно считываем данные
									{
										var packnum = reader.GetValue(0);
										Console.WriteLine("Коду маркировки соответствует номер короба: " + packnum.ToString());
										//Отправка ответа клиенту
										await stream.WriteAsync(Encoding.UTF8.GetBytes(packnum.ToString())); 
									}

								}
								else
								{
									Console.WriteLine("Не найдено соответствий.");
									//Отправка ответа клиенту
									await stream.WriteAsync(Encoding.UTF8.GetBytes("null"));
								}

							}

						}
						catch (Exception ex)
						{
							Console.WriteLine(ex.Message);
						}

						Console.WriteLine($"Клиенту отправлен ответ.");

						stream.Close();
					}
					tcpClient.Close();
					Console.WriteLine($"Клиент отключился от сервера.");

				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			finally
			{
				server.Stop();
			}
		}

	}
}
