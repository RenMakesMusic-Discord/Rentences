using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rentences.Domain.Definitions.Commands
{
    public interface ICommand
    {
        string Name { get; }
        Task ExecuteAsync(SocketMessage source, params string[] args );
    }

}
