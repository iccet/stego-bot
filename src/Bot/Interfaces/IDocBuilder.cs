using Bot.Services;

namespace Bot.Interfaces
{
    using StateMachine = Stateless.StateMachine<State, Command>;
    public interface IDocBuilder
    {
        string Build(StateMachine machine);
    }
}